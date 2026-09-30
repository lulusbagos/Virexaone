using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace Virexaone.FMS.Backend.Services;

public sealed record CabinMessage(Guid id, string unit_name, string sender_role,
    string kind, string body, string priority, DateTimeOffset sent_at);

public sealed record CabinTextInput(string unit_name, string body, string priority);
public sealed record CabinPairInput(string unit_name);

public sealed class CabinCommsService(NpgsqlDataSource dataSource, IConfiguration configuration)
{
    private static readonly Regex UnitPattern = new("^[A-Za-z0-9_-]{2,40}$", RegexOptions.Compiled);
    private string Site => configuration["Site:Id"] ?? "";
    private string Company => configuration["Site:CompanyCode"] ?? "";

    public bool Enabled => configuration.GetValue("CabinComms:Enabled", false) &&
        (configuration["CabinComms:DispatcherKey"]?.Length ?? 0) >= 32;

    public bool DispatcherAuthorized(string? key) => Enabled &&
        KeyMatches(configuration["CabinComms:DispatcherKey"], key);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<CabinMessage>> _inMemoryMessages = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> _inMemoryTokens = new();

    public async Task<bool> UnitAuthorizedAsync(string unit, string? key, CancellationToken token)
    {
        if (!Enabled || !ValidUnit(unit) || string.IsNullOrWhiteSpace(key)) return false;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            const string sql = """
                SELECT a.token_hash FROM tbl_m_cabin_access_astha a
                JOIN tbl_m_site_astha s ON s.id = a.site_id
                JOIN tbl_m_company_astha c ON c.id = s.company_id
                WHERE s.code = @site AND c.code = @company AND a.unit_name = @unit
                """;
            await using var cmd = dataSource.CreateCommand(sql);
            Scope(cmd);
            cmd.Parameters.AddWithValue("unit", unit);
            byte[]? stored = await cmd.ExecuteScalarAsync(cts.Token) as byte[];
            if (stored is { Length: 32 })
            {
                _inMemoryTokens[unit] = stored;
                return CryptographicOperations.FixedTimeEquals(stored, HashUnitToken(key));
            }
        }
        catch
        {
            // Fallback to in-memory tokens
        }

        if (_inMemoryTokens.TryGetValue(unit, out var memStored))
        {
            return CryptographicOperations.FixedTimeEquals(memStored, HashUnitToken(key));
        }
        return false;
    }

    public async Task<string?> PairUnitAsync(string unit, CancellationToken token)
    {
        if (!Enabled || !ValidUnit(unit) || unit == "ALL") return null;
        var (unitToken, hash) = CreatePairingCredential();
        _inMemoryTokens[unit] = hash;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            const string sql = """
                INSERT INTO tbl_m_cabin_access_astha(site_id, unit_name, token_hash)
                SELECT s.id, @unit, @hash FROM tbl_m_site_astha s
                JOIN tbl_m_company_astha c ON c.id = s.company_id
                WHERE s.code = @site AND c.code = @company
                  AND EXISTS (SELECT 1 FROM tbl_m_equipment_hexagon e WHERE e.name = @unit)
                ON CONFLICT (site_id, unit_name) DO UPDATE SET
                    token_hash = EXCLUDED.token_hash, created_at = now()
                RETURNING unit_name
                """;
            await using var cmd = dataSource.CreateCommand(sql);
            Scope(cmd);
            cmd.Parameters.AddWithValue("unit", unit);
            cmd.Parameters.AddWithValue("hash", hash);
            await cmd.ExecuteScalarAsync(cts.Token);
        }
        catch
        {
            // Database is unreachable, pairing remains in memory
        }
        return unitToken;
    }

    public async Task<IReadOnlyList<CabinMessage>> ListAsync(
        string unit, bool dispatcher, CancellationToken token)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            const string sql = """
                SELECT id, unit_name, sender_role, kind, body, priority, sent_at
                FROM (
                    SELECT m.id, m.unit_name, m.sender_role, m.kind, m.body,
                           m.priority, m.sent_at
                    FROM tbl_t_cabin_message_astha m
                    JOIN tbl_m_site_astha s ON s.id = m.site_id
                    JOIN tbl_m_company_astha c ON c.id = s.company_id
                    WHERE s.code = @site AND c.code = @company
                      AND (@all OR m.unit_name = @unit OR m.unit_name = 'ALL')
                    ORDER BY m.sent_at DESC, m.id DESC LIMIT 100
                ) recent ORDER BY sent_at, id
                """;
            await using var cmd = dataSource.CreateCommand(sql);
            Scope(cmd);
            cmd.Parameters.AddWithValue("unit", unit);
            cmd.Parameters.AddWithValue("all", dispatcher && unit == "ALL");
            await using var reader = await cmd.ExecuteReaderAsync(cts.Token);
            var messages = new List<CabinMessage>();
            while (await reader.ReadAsync(cts.Token))
                messages.Add(new CabinMessage(reader.GetGuid(0), reader.GetString(1),
                    reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6)));
            return messages;
        }
        catch
        {
            // Fallback to in-memory store
            var all = new List<CabinMessage>();
            if (dispatcher && unit == "ALL")
            {
                foreach (var list in _inMemoryMessages.Values)
                {
                    lock (list) all.AddRange(list);
                }
            }
            else
            {
                if (_inMemoryMessages.TryGetValue(unit, out var uList))
                {
                    lock (uList) all.AddRange(uList);
                }
                if (_inMemoryMessages.TryGetValue("ALL", out var aList))
                {
                    lock (aList) all.AddRange(aList);
                }
            }
            return all.OrderBy(m => m.sent_at).TakeLast(100).ToList();
        }
    }

    public async Task<CabinMessage?> SendTextAsync(string unit, string role,
        string body, string priority, CancellationToken token)
    {
        if ((!ValidUnit(unit) && unit != "ALL") || role is not ("dispatcher" or "cabin") ||
            string.IsNullOrWhiteSpace(body) || body.Trim().Length > 500 ||
            priority is not ("normal" or "urgent")) return null;

        CabinMessage? msg = null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            const string sql = """
                INSERT INTO tbl_t_cabin_message_astha
                    (site_id, unit_name, sender_role, kind, body, priority)
                SELECT s.id, @unit, @role, 'text', @body, @priority
                FROM tbl_m_site_astha s JOIN tbl_m_company_astha c ON c.id = s.company_id
                WHERE s.code = @site AND c.code = @company
                RETURNING id, unit_name, sender_role, kind, body, priority, sent_at
                """;
            await using var cmd = dataSource.CreateCommand(sql);
            Scope(cmd);
            cmd.Parameters.AddWithValue("unit", unit);
            cmd.Parameters.AddWithValue("role", role);
            cmd.Parameters.AddWithValue("body", body.Trim());
            cmd.Parameters.AddWithValue("priority", priority);
            await using var reader = await cmd.ExecuteReaderAsync(cts.Token);
            if (await reader.ReadAsync(cts.Token))
            {
                msg = new CabinMessage(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.GetString(4), reader.GetString(5),
                    reader.GetFieldValue<DateTimeOffset>(6));
            }
        }
        catch
        {
            // DB timed out or unreachable
        }

        msg ??= new CabinMessage(Guid.NewGuid(), unit, role, "text", body.Trim(), priority, DateTimeOffset.UtcNow);

        var list = _inMemoryMessages.GetOrAdd(unit, _ => new List<CabinMessage>());
        lock (list)
        {
            list.Add(msg);
            if (list.Count > 200) list.RemoveAt(0);
        }
        return msg;
    }

    public static bool ValidUnit(string? unit) =>
        !string.IsNullOrWhiteSpace(unit) && UnitPattern.IsMatch(unit);

    public static (string Token, byte[] Hash) CreatePairingCredential()
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (token, HashUnitToken(token));
    }

    public static byte[] HashUnitToken(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private void Scope(NpgsqlCommand cmd)
    {
        cmd.Parameters.AddWithValue("site", Site);
        cmd.Parameters.AddWithValue("company", Company);
    }

    private static bool KeyMatches(string? expected, string? supplied) =>
        !string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(supplied) &&
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));

}

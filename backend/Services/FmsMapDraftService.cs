using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NetTopologySuite.Geometries;
using Npgsql;
using NpgsqlTypes;
using Virexaone.FMS.Backend.Utils;

namespace Virexaone.FMS.Backend.Services;

public sealed record MapPoint(double easting, double northing, double elevation);

public sealed class MapDraftInput
{
    public string code { get; set; } = "";
    public string name { get; set; } = "";
    public string feature_type { get; set; } = "";
    public string shape_kind { get; set; } = "";
    public List<MapPoint> points { get; set; } = [];
    public double width_m { get; set; }
    public string color_hex { get; set; } = "#4FD0B4";
    public string updated_at { get; set; } = "";
}

public sealed record MapDraftItem(Guid id, string code, string name, string feature_type,
    string shape_kind, List<MapPoint> points, double? width_m, double? radius_m,
    string color_hex, string status, DateTimeOffset updated_at);

public sealed class FmsMapDraftService(NpgsqlDataSource dataSource, IConfiguration configuration)
{
    private static readonly Regex CodePattern = new("^[A-Za-z0-9_-]{1,40}$", RegexOptions.Compiled);
    private static readonly Regex ColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly HashSet<string> AreaTypes = ["loading", "front", "disposal", "stockpile"];

    public async Task<IReadOnlyList<MapDraftItem>> ListAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT d.id, d.code, d.name, d.feature_type, d.shape_kind, d.points::text,
                   d.width_m, d.radius_m, d.color_hex, d.status, d.updated_at
            FROM tbl_m_map_draft_astha d
            JOIN tbl_m_site_astha s ON s.id = d.site_id
            JOIN tbl_m_company_astha c ON c.id = s.company_id
            WHERE s.code = @site AND c.code = @company AND d.status = 'draft'
            ORDER BY d.feature_type, d.name, d.code
            """;
        await using var command = dataSource.CreateCommand(sql);
        AddSiteParameters(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<MapDraftItem>();
        while (await reader.ReadAsync(cancellationToken))
            items.Add(ReadItem(reader));
        return items;
    }

    public async Task<MapDraftItem?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT d.id, d.code, d.name, d.feature_type, d.shape_kind, d.points::text,
                   d.width_m, d.radius_m, d.color_hex, d.status, d.updated_at
            FROM tbl_m_map_draft_astha d
            JOIN tbl_m_site_astha s ON s.id = d.site_id
            JOIN tbl_m_company_astha c ON c.id = s.company_id
            WHERE d.id = @id AND s.code = @site AND c.code = @company AND d.status = 'draft'
            """;
        await using var command = dataSource.CreateCommand(sql);
        AddSiteParameters(command);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadItem(reader) : null;
    }

    public async Task<(MapDraftItem? Item, string? Error)> SaveAsync(
        Guid? id, MapDraftInput input, CancellationToken cancellationToken)
    {
        string? error = Validate(input, out string wkt, out double? radius);
        if (error != null) return (null, error);
        if (id.HasValue && !DateTimeOffset.TryParse(input.updated_at, out _))
            return (null, "revision_required");

        const string insert = """
            WITH candidate AS (
                SELECT ST_GeomFromText(@wkt, @epsg) AS shape
            )
            INSERT INTO tbl_m_map_draft_astha
                (site_id, code, name, feature_type, shape_kind, points, shape,
                 width_m, radius_m, color_hex)
            SELECT s.id, @code, @name, @type, @kind, @points, candidate.shape,
                   @width, @radius, @color
            FROM tbl_m_site_astha s
            JOIN tbl_m_company_astha c ON c.id = s.company_id
            CROSS JOIN candidate
            WHERE s.code = @site AND c.code = @company AND ST_IsValid(candidate.shape)
            RETURNING id, code, name, feature_type, shape_kind, points::text,
                      width_m, radius_m, color_hex, status, updated_at
            """;
        const string update = """
            UPDATE tbl_m_map_draft_astha d SET
                code = @code, name = @name, feature_type = @type,
                shape_kind = @kind, points = @points,
                shape = ST_GeomFromText(@wkt, @epsg), width_m = @width,
                radius_m = @radius, color_hex = @color, updated_at = now()
            FROM tbl_m_site_astha s
            JOIN tbl_m_company_astha c ON c.id = s.company_id
            WHERE d.id = @id AND d.site_id = s.id AND s.code = @site
              AND c.code = @company AND d.status = 'draft'
              AND d.updated_at = @revision
              AND ST_IsValid(ST_GeomFromText(@wkt, @epsg))
            RETURNING d.id, d.code, d.name, d.feature_type, d.shape_kind,
                      d.points::text, d.width_m, d.radius_m, d.color_hex,
                      d.status, d.updated_at
            """;
        try
        {
            await using var command = dataSource.CreateCommand(id.HasValue ? update : insert);
            AddSiteParameters(command);
            command.Parameters.AddWithValue("code", input.code.Trim());
            command.Parameters.AddWithValue("name", input.name.Trim());
            command.Parameters.AddWithValue("type", input.feature_type);
            command.Parameters.AddWithValue("kind", input.shape_kind);
            command.Parameters.AddWithValue("points", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(input.points));
            command.Parameters.AddWithValue("wkt", wkt);
            command.Parameters.AddWithValue("epsg", GeoTransform.UtmEpsg);
            command.Parameters.AddWithValue("width", input.width_m > 0 ? input.width_m : DBNull.Value);
            command.Parameters.AddWithValue("radius", (object?)radius ?? DBNull.Value);
            command.Parameters.AddWithValue("color", input.color_hex.ToUpperInvariant());
            if (id.HasValue)
            {
                command.Parameters.AddWithValue("id", id.Value);
                command.Parameters.AddWithValue("revision", DateTimeOffset.Parse(input.updated_at).ToUniversalTime());
            }
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? (ReadItem(reader), null)
                : (null, id.HasValue ? "draft_changed_or_missing" : "site_missing_or_invalid_shape");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return (null, "code_already_exists");
        }
    }

    public async Task<bool> RetireAsync(Guid id, DateTimeOffset revision, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE tbl_m_map_draft_astha d SET status = 'retired', updated_at = now()
            FROM tbl_m_site_astha s
            JOIN tbl_m_company_astha c ON c.id = s.company_id
            WHERE d.id = @id AND d.site_id = s.id AND s.code = @site
              AND c.code = @company AND d.status = 'draft' AND d.updated_at = @revision
            """;
        await using var command = dataSource.CreateCommand(sql);
        AddSiteParameters(command);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("revision", revision.ToUniversalTime());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private void AddSiteParameters(NpgsqlCommand command)
    {
        command.Parameters.AddWithValue("site", configuration["Site:Id"] ?? "");
        command.Parameters.AddWithValue("company", configuration["Site:CompanyCode"] ?? "");
    }

    private static MapDraftItem ReadItem(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
        reader.GetString(3), reader.GetString(4),
        JsonSerializer.Deserialize<List<MapPoint>>(reader.GetString(5)) ?? [],
        reader.IsDBNull(6) ? null : Convert.ToDouble(reader.GetDecimal(6)),
        reader.IsDBNull(7) ? null : Convert.ToDouble(reader.GetDecimal(7)),
        reader.GetString(8).Trim(), reader.GetString(9), reader.GetFieldValue<DateTimeOffset>(10));

    private static string? Validate(MapDraftInput input, out string wkt, out double? radius)
    {
        wkt = "";
        radius = null;
        if (!CodePattern.IsMatch(input.code?.Trim() ?? "") || string.IsNullOrWhiteSpace(input.name) ||
            input.name.Length > 100 || !ColorPattern.IsMatch(input.color_hex ?? "")) return "invalid_metadata";
        if (input.points == null || input.points.Count > 200 || input.points.Any(point => point is null))
            return "invalid_points";
        bool road = input.feature_type == "road" && input.shape_kind == "line";
        bool note = input.feature_type == "note" && input.shape_kind == "text";
        bool area = AreaTypes.Contains(input.feature_type) &&
            (input.shape_kind == "rectangle" || input.shape_kind == "circle" || input.shape_kind == "polygon");
        if (!road && !note && !area) return "invalid_feature_shape";
        int needed = road ? 2 : note ? 1 : input.shape_kind == "polygon" ? 3 : 2;
        if (input.points.Count < needed ||
            (input.shape_kind is "rectangle" or "circle") && input.points.Count != 2 ||
            note && input.points.Count != 1) return "invalid_points";
        if (road && (!double.IsFinite(input.width_m) ||
            input.width_m < 2 || input.width_m > 100)) return "invalid_road_width";
        if (!road && input.width_m != 0) return "width_only_for_road";
        foreach (MapPoint point in input.points)
            if (!double.IsFinite(point.easting) || !double.IsFinite(point.northing) ||
                !double.IsFinite(point.elevation) ||
                Math.Abs(point.easting - GeoTransform.RefEasting) > 25000 ||
                Math.Abs(point.northing - GeoTransform.RefNorthing) > 25000)
                return "point_outside_site";

        static double DistanceSquared(MapPoint a, MapPoint b) =>
            Math.Pow(a.easting - b.easting, 2) + Math.Pow(a.northing - b.northing, 2);
        for (int i = 1; i < input.points.Count; i++)
            if (DistanceSquared(input.points[i - 1], input.points[i]) < 0.000001)
                return "invalid_geometry";
        if (road && input.points.Zip(input.points.Skip(1), (a, b) =>
                Math.Sqrt(DistanceSquared(a, b))).Sum() < 1)
            return "invalid_geometry";
        if (input.shape_kind == "rectangle" &&
            (Math.Abs(input.points[1].easting - input.points[0].easting) < 1 ||
             Math.Abs(input.points[1].northing - input.points[0].northing) < 1))
            return "invalid_geometry";
        if (input.shape_kind == "polygon")
        {
            if (DistanceSquared(input.points[0], input.points[^1]) < 0.000001)
                return "invalid_geometry";
            double twiceArea = 0;
            for (int i = 0; i < input.points.Count; i++)
            {
                MapPoint a = input.points[i], b = input.points[(i + 1) % input.points.Count];
                twiceArea += a.easting * b.northing - b.easting * a.northing;
            }
            if (Math.Abs(twiceArea) < 2) return "invalid_geometry";
        }

        List<MapPoint> outline = input.points;
        if (input.shape_kind == "rectangle")
        {
            var a = outline[0]; var b = outline[1];
            outline = [a, new(b.easting, a.northing, a.elevation), b,
                new(a.easting, b.northing, b.elevation)];
        }
        else if (input.shape_kind == "circle")
        {
            var a = outline[0]; var b = outline[1];
            radius = Math.Sqrt(Math.Pow(b.easting - a.easting, 2) + Math.Pow(b.northing - a.northing, 2));
            if (radius < 1 || radius > 5000) return "invalid_radius";
            double circleRadius = radius.Value;
            outline = Enumerable.Range(0, 48).Select(i =>
                new MapPoint(a.easting + circleRadius * Math.Cos(2 * Math.PI * i / 48),
                    a.northing + circleRadius * Math.Sin(2 * Math.PI * i / 48), a.elevation)).ToList();
        }

        if (!road && !note)
        {
            Coordinate[] ring = outline.Select(point => new Coordinate(point.easting, point.northing))
                .Append(new Coordinate(outline[0].easting, outline[0].northing)).ToArray();
            if (!new GeometryFactory().CreatePolygon(ring).IsValid)
                return "invalid_geometry";
        }

        static string Coordinate(MapPoint point) =>
            point.easting.ToString("R", CultureInfo.InvariantCulture) + " " +
            point.northing.ToString("R", CultureInfo.InvariantCulture);
        if (road)
            wkt = "LINESTRING(" + string.Join(",", outline.Select(Coordinate)) + ")";
        else if (note)
            wkt = "POINT(" + Coordinate(outline[0]) + ")";
        else
            wkt = "POLYGON((" + string.Join(",", outline.Select(Coordinate).Append(Coordinate(outline[0]))) + "))";
        return null;
    }
}

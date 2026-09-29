using Npgsql;

namespace Virexaone.FMS.Backend.Services;

public sealed record RoadAuditItem(long road_id, string start_name, string end_name,
    long? distance_m, long? lane_width_m, long? speed_max_kmh, string[] issues);

public sealed record RoadAuditSummary(int total_roads, int roads_with_issues, int priority_roads,
    int invalid_distance, int missing_start, int missing_end, int same_endpoint,
    int missing_width, int missing_speed, int missing_trajectory);

public sealed record RoadAuditResult(string status, DateTimeOffset generated_at,
    RoadAuditSummary summary, int matching_issues, int offset, int limit,
    IReadOnlyList<RoadAuditItem> data);

public sealed class FmsRoadAuditService(NpgsqlDataSource dataSource)
{
    public async Task<RoadAuditResult> GetAsync(string? search, string? scope, int offset, int limit,
        CancellationToken cancellationToken)
    {
        search = search?.Trim() ?? "";
        if (search.Length > 80) search = search[..80];
        bool allIssues = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase);
        offset = Math.Clamp(offset, 0, 100000);
        limit = Math.Clamp(limit, 10, 100);

        const string sql = """
            SELECT r.id, r.distance, r.lane_width, r.speed_max, r.trajectory,
                   r.start_location_id, sl.id, sl.name,
                   r.end_location_id, el.id, el.name
            FROM tbl_m_roads_hexagon r
            LEFT JOIN tbl_m_locations_hexagon sl ON sl.id = r.start_location_id
            LEFT JOIN tbl_m_locations_hexagon el ON el.id = r.end_location_id
            ORDER BY r.id
            """;
        var items = new List<RoadAuditItem>(limit);
        int total = 0, affected = 0, priority = 0, matching = 0;
        int invalidDistance = 0, missingStart = 0, missingEnd = 0;
        int sameEndpoint = 0, missingWidth = 0, missingSpeed = 0, missingTrajectory = 0;

        await using var command = dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            total++;
            long roadId = reader.GetInt64(0);
            long? distance = reader.IsDBNull(1) ? null : reader.GetInt64(1);
            long? width = reader.IsDBNull(2) ? null : reader.GetInt64(2);
            long? speed = reader.IsDBNull(3) ? null : reader.GetInt64(3);
            string? trajectory = reader.IsDBNull(4) ? null : reader.GetString(4);
            long? startId = reader.IsDBNull(5) ? null : reader.GetInt64(5);
            long? endId = reader.IsDBNull(8) ? null : reader.GetInt64(8);
            bool startMissing = startId == null || reader.IsDBNull(6);
            bool endMissing = endId == null || reader.IsDBNull(9);
            string startName = reader.IsDBNull(7) ?
                (startId == null ? "-" : $"ID {startId}") : reader.GetString(7);
            string endName = reader.IsDBNull(10) ?
                (endId == null ? "-" : $"ID {endId}") : reader.GetString(10);

            var issues = new List<string>(7);
            if (distance is null or <= 0) { issues.Add("invalid_distance"); invalidDistance++; }
            if (startMissing) { issues.Add("missing_start"); missingStart++; }
            if (endMissing) { issues.Add("missing_end"); missingEnd++; }
            if (startId != null && startId == endId) { issues.Add("same_endpoint"); sameEndpoint++; }
            if (width is null or <= 0) { issues.Add("missing_width"); missingWidth++; }
            if (speed is null or <= 0) { issues.Add("missing_speed"); missingSpeed++; }
            if (string.IsNullOrWhiteSpace(trajectory)) { issues.Add("missing_trajectory"); missingTrajectory++; }
            if (issues.Count == 0) continue;

            affected++;
            bool isPriority = issues.Contains("invalid_distance") || issues.Contains("missing_start") ||
                issues.Contains("missing_end") || issues.Contains("same_endpoint") ||
                issues.Contains("missing_trajectory");
            if (isPriority) priority++;
            if (!allIssues && !isPriority) continue;
            if (search.Length > 0 && !roadId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !startName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !endName.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            if (matching++ < offset || items.Count >= limit) continue;
            items.Add(new RoadAuditItem(roadId, startName, endName, distance, width, speed,
                issues.ToArray()));
        }

        return new RoadAuditResult("success", DateTimeOffset.UtcNow,
            new RoadAuditSummary(total, affected, priority, invalidDistance, missingStart, missingEnd,
                sameEndpoint, missingWidth, missingSpeed, missingTrajectory),
            matching, offset, limit, items);
    }
}

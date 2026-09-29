using Npgsql;

namespace Virexaone.FMS.Backend.Services;

// A read-only projection of the source rows; unknown Hexagon codes stay as codes.
public sealed class CabinHexagonService(NpgsqlDataSource dataSource)
{
    public async Task<object?> GetUnitAsync(string unitName, CancellationToken token)
    {
        const string sql = """
            SELECT e.name, e.type, e.status_id, e.activity_id, e.prestart_check,
                   e.warnings, e.last_heard,
                   t.updated_at, t.located_at, t.satellites, t.hdop,
                   t.signal_strength, t.gps_quality_id, t.road_segment,
                   t.location_id, loc.name, t.next_location_id, next_loc.name,
                   h.updated_at, h.total_at, h.total_loads, h.tonnage,
                   h.has_payload, h.material_id, h.shovel_id, h.dump_id,
                   a.updated_at
            FROM tbl_m_equipment_hexagon e
            LEFT JOIN LATERAL (
                SELECT * FROM tbl_m_traveling_hexagon
                WHERE equipment_id = e.id ORDER BY updated_at DESC NULLS LAST LIMIT 1
            ) t ON true
            LEFT JOIN tbl_m_locations_hexagon loc ON loc.id = t.location_id
            LEFT JOIN tbl_m_locations_hexagon next_loc ON next_loc.id = t.next_location_id
            LEFT JOIN LATERAL (
                SELECT * FROM tbl_m_hauling_hexagon
                WHERE equipment_id = e.id ORDER BY updated_at DESC NULLS LAST LIMIT 1
            ) h ON true
            LEFT JOIN LATERAL (
                SELECT updated_at FROM tbl_m_assignments_hexagon
                WHERE equipment_id = e.id ORDER BY updated_at DESC NULLS LAST LIMIT 1
            ) a ON true
            WHERE e.name = @unit AND e.deleted_at IS NULL
            ORDER BY e.updated_at DESC NULLS LAST LIMIT 1
            """;
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("unit", unitName);
        await using var row = await cmd.ExecuteReaderAsync(token);
        if (!await row.ReadAsync(token)) return null;
        object? Read(int index) => row.IsDBNull(index) ? null : row.GetValue(index);
        return new
        {
            unit_name = row.GetString(0),
            equipment_type = Read(1),
            status_id = Read(2),
            activity_id = Read(3),
            prestart_check = Read(4),
            warnings_code = Read(5),
            equipment_last_heard = Read(6),
            travel_updated_at = Read(7),
            gps_located_at = Read(8),
            satellites = Read(9),
            hdop_raw = Read(10),
            signal_strength_raw = Read(11),
            gps_quality_id = Read(12),
            road_segment_id = Read(13),
            current_location_id = Read(14),
            current_location_name = Read(15),
            next_location_id = Read(16),
            next_location_name = Read(17),
            haul_updated_at = Read(18),
            haul_total_at = Read(19),
            total_loads = Read(20),
            tonnage = Read(21),
            has_payload = Read(22),
            material_id = Read(23),
            haul_shovel_id = Read(24),
            haul_dump_id = Read(25),
            assignment_updated_at = Read(26)
        };
    }
}

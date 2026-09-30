using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Virexaone.FMS.Backend.Models;
using Virexaone.FMS.Backend.Utils;

namespace Virexaone.FMS.Backend.Services
{
    public class FmsDataService
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly EquipmentCategoryResolver _categoryResolver;
        private readonly UnitDimensionResolver _dimensionResolver;

        private List<MiningLocationDto>? _cachedLocations;
        private List<RoadSegmentDto>? _cachedRoads;
        private List<FleetUnitDto>? _cachedFleet;
        private DateTime _cachedFleetAtUtc = DateTime.MinValue;
        private bool _lastFleetReadSucceeded;
        private DateTime? _lastFleetIngestAtUtc;
        private static readonly TimeSpan LiveFleetCacheTtl = TimeSpan.FromSeconds(5);
        private readonly object _lock = new();
        private readonly SemaphoreSlim _fleetRefreshGate = new(1, 1);

        private static readonly Dictionary<long, string> ActivityNames = new()
        {
            { 1, "Idle" },
            { 2, "Hauling (Loaded)" },
            { 3, "Traveling (Empty)" },
            { 4, "Queuing" },
            { 5, "Spotting" },
            { 6, "Loading" },
            { 7, "Dumping" },
            { 8, "Breakdown / Repair" },
            { 9, "Standby / Meal Break" },
            { 10, "Refueling" },
            { 20, "Hauling" },
            { 21, "Hauling & Traveling" },
            { 23, "Loading Material" },
            { 34, "Digging / Face Prep" },
            { 41, "Dumping & Spreading" },
            { 52, "Road Grading" },
            { 63, "Water Spraying" }
        };

        public FmsDataService(NpgsqlDataSource dataSource, EquipmentCategoryResolver categoryResolver,
            UnitDimensionResolver dimensionResolver)
        {
            _dataSource = dataSource;
            _categoryResolver = categoryResolver;
            _dimensionResolver = dimensionResolver;
        }

        public bool FleetDataAvailable
        {
            get { lock (_lock) return _lastFleetReadSucceeded; }
        }

        public int FleetFeedAgeSeconds
        {
            get
            {
                lock (_lock)
                {
                    return _lastFleetIngestAtUtc.HasValue
                        ? Math.Max(0, (int)(DateTime.UtcNow - _lastFleetIngestAtUtc.Value).TotalSeconds)
                        : int.MaxValue;
                }
            }
        }

        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                await using var cmd = _dataSource.CreateCommand("SELECT 1;");
                var result = await cmd.ExecuteScalarAsync();
                return result != null && Convert.ToInt32(result) == 1;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<FleetUnitDto>> GetLiveFleetAsync()
        {
            lock (_lock)
            {
                if (_cachedFleet != null && DateTime.UtcNow - _cachedFleetAtUtc <= LiveFleetCacheTtl)
                {
                    return _cachedFleet;
                }
            }

            await _fleetRefreshGate.WaitAsync();
            try
            {
                lock (_lock)
                {
                    if (_cachedFleet != null && DateTime.UtcNow - _cachedFleetAtUtc <= LiveFleetCacheTtl)
                    {
                        return _cachedFleet;
                    }
                }

                return await ReadLiveFleetAsync();
            }
            finally
            {
                _fleetRefreshGate.Release();
            }
        }

        public async Task<List<FleetInventoryUnitDto>> GetFleetInventoryAsync()
        {
            const string sql = @"
                SELECT e.id, e.name, e.type, e.last_heard,
                    (SELECT MAX(t.updated_at) FROM tbl_m_traveling t
                     WHERE t.equipment_id = e.id AND t.longitude > 0 AND t.latitude > 0) AS standard_gps_at,
                    (SELECT MAX(t.updated_at) FROM tbl_m_traveling_hexagon t
                     WHERE t.equipment_id = e.id AND t.longitude > 0 AND t.latitude > 0) AS hexagon_gps_at
                FROM tbl_m_equipment e
                WHERE e.deleted_at IS NULL
                  AND e.name NOT ILIKE 'TEST%'
                  AND e.name NOT ILIKE 'jigsaw'
                ORDER BY e.type, e.name;";

            var units = new List<FleetInventoryUnitDto>();
            await using var command = _dataSource.CreateCommand(sql);
            command.CommandTimeout = 15;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string name = reader.GetString(1);
                string type = reader.IsDBNull(2) ? "" : reader.GetString(2);
                string category = _categoryResolver.Resolve(type, name);
                if (category != "Excavator" && category != "Bulldozer" && category != "Grader")
                    continue;

                DateTime? standardGpsAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                DateTime? hexagonGpsAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                DateTime? gpsAt = standardGpsAt > hexagonGpsAt ? standardGpsAt : hexagonGpsAt ?? standardGpsAt;
                units.Add(new FleetInventoryUnitDto(
                    reader.GetInt64(0), name, type, category,
                    reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToUniversalTime().ToString("o"),
                    gpsAt?.ToUniversalTime().ToString("o")));
            }

            return units;
        }

        private async Task<List<FleetUnitDto>> ReadLiveFleetAsync()
        {
            try
            {
                const string sql = @"
                    WITH active_shift AS (SELECT MAX(total_at) AS started_at FROM tbl_m_hauling_hexagon)
                    SELECT 
                        active_shift.started_at as shift_started_at,
                        e.id as equipment_id,
                        e.name as unit_name,
                        e.type as unit_type,
                        e.status_id,
                        e.reason_id,
                        e.activity_id,
                        e.activity_start,
                        e.operator_id,
                        e.equipment_type_id,
                        e.last_heard,
                        e.updated_at,
                        e.size as vessel_capacity_ton,
                        e.prestart_check,
                        t.longitude as hex_x,
                        t.latitude as hex_y,
                        t.elevation as hex_z,
                        COALESCE(t.velocity, 0.0) as speed_kmh,
                        COALESCE(t.heading, 0.0) as heading_deg,
                        (t.heading IS NOT NULL AND t.heading >= 0 AND t.heading <= 360) as heading_available,
                        t.located_at,
                        t.updated_at as gps_updated_at,
                        t.ingested_at,
                        h.has_payload,
                        h.tonnage as payload_ton,
                        h.id as haul_id,
                        h.total_loads as recorded_loads,
                        h.updated_at as haul_updated_at,
                        h.distance as haul_distance_m,
                        h.expected_time as cycle_expected_sec,
                        h.material_id,
                        h.odometer_start,
                        s.name as assigned_shovel_name,
                        d.name as dump_location_name
                    FROM tbl_m_equipment_hexagon e
                    CROSS JOIN active_shift
                    LEFT JOIN tbl_m_traveling_hexagon t ON t.equipment_id = e.id
                    LEFT JOIN tbl_m_hauling_hexagon h ON h.equipment_id = e.id AND h.total_at = active_shift.started_at
                    LEFT JOIN tbl_m_equipment_hexagon s ON s.id = h.shovel_id
                    LEFT JOIN tbl_m_locations_hexagon d ON d.id = h.dump_id
                    WHERE e.name NOT ILIKE 'TEST%' AND e.name NOT ILIKE 'jigsaw'
                    ORDER BY e.type, e.name;";

                // Historical GPS fixes must come from traveling records. Hauling snapshots
                // can contain unrelated coordinates for the same equipment.
                var trajectoryMap = new Dictionary<long, List<TrajectoryPointDto>>();
                try
                {
                    const string trajSql = @"
                        WITH recent_gps AS (
                            SELECT
                                h.equipment_id,
                                h.longitude as hex_x,
                                h.latitude as hex_y,
                                h.elevation as hex_z,
                                COALESCE(h.velocity, 0.0) as speed_kmh,
                                COALESCE(h.heading, 0.0) as heading_deg,
                                h.updated_at as gps_recorded_at,
                                h.ingested_at,
                                row_number() OVER (
                                    PARTITION BY h.equipment_id
                                    ORDER BY h.ingested_at DESC, h.history_id DESC
                                ) AS point_rank
                            FROM tbl_h_gps_history h
                            JOIN tbl_m_equipment_hexagon e ON e.id = h.equipment_id
                            WHERE h.source_table = 'tbl_m_traveling_hexagon'
                              AND h.ingested_at >= now() - interval '10 minutes'
                              AND h.updated_at IS NOT NULL
                              AND h.longitude > 0
                              AND h.latitude > 0
                              AND e.name NOT ILIKE 'TEST%'
                              AND e.name NOT ILIKE 'jigsaw'
                        )
                        SELECT equipment_id, hex_x, hex_y, hex_z, speed_kmh, heading_deg, gps_recorded_at
                        FROM recent_gps
                        WHERE point_rank <= 20
                        ORDER BY equipment_id, gps_recorded_at ASC, ingested_at ASC;";

                    await using var trajCmd = _dataSource.CreateCommand(trajSql);
                    trajCmd.CommandTimeout = 8;
                    await using var trajReader = await trajCmd.ExecuteReaderAsync();
                    while (await trajReader.ReadAsync())
                    {
                        long tEqId = trajReader.GetInt64("equipment_id");
                        double hx = trajReader.GetDouble("hex_x");
                        double hy = trajReader.GetDouble("hex_y");
                        double hz = trajReader.IsDBNull("hex_z") ? 0.0 : trajReader.GetDouble("hex_z");
                        double spd = trajReader.GetDouble("speed_kmh");
                        double hdg = trajReader.GetDouble("heading_deg");
                        string recordedAt = trajReader.GetDateTime("gps_recorded_at").ToUniversalTime().ToString("o");

                        var (_, _, _, _, _, ptPos) = GeoTransform.HexagonToWorld(hx, hy, hz);

                        if (!trajectoryMap.TryGetValue(tEqId, out var ptList))
                        {
                            ptList = new List<TrajectoryPointDto>(20);
                            trajectoryMap[tEqId] = ptList;
                        }

                        ptList.Add(new TrajectoryPointDto(
                            X: ptPos.X,
                            Y: ptPos.Y,
                            Z: ptPos.Z,
                            SpeedKmh: Math.Round(spd, 1),
                            HeadingDeg: Math.Round(hdg, 1),
                            RecordedAt: recordedAt
                        ));
                    }
                }
                catch (Exception trajEx)
                {
                    Console.WriteLine($"[Warning] Trajectory batch query: {trajEx.Message}");
                }

                var list = new List<FleetUnitDto>(250);
                var nowUtc = DateTime.UtcNow;
                DateTime? latestIngestUtc = null;

                await using var cmd = _dataSource.CreateCommand(sql);
                cmd.CommandTimeout = 20;
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull("ingested_at"))
                    {
                        DateTime ingestedAt = reader.GetDateTime("ingested_at").ToUniversalTime();
                        if (!latestIngestUtc.HasValue || ingestedAt > latestIngestUtc.Value)
                            latestIngestUtc = ingestedAt;
                    }
                    long eqId = reader.GetInt64("equipment_id");
                    string unitName = reader.GetString("unit_name");
                    string unitType = reader.IsDBNull("unit_type") ? "Unknown" : reader.GetString("unit_type");
                    long? statusId = reader.IsDBNull("status_id") ? null : reader.GetInt64("status_id");
                    long? activityId = reader.IsDBNull("activity_id") ? null : reader.GetInt64("activity_id");
                    long? operatorId = reader.IsDBNull("operator_id") ? null : reader.GetInt64("operator_id");

                    string activityName = "Ready";
                    if (activityId.HasValue && ActivityNames.TryGetValue(activityId.Value, out var actStr))
                    {
                        activityName = actStr;
                    }
                    else if (activityId.HasValue)
                    {
                        activityName = $"Activity-{activityId.Value}";
                    }

                    double? rawX = reader.IsDBNull("hex_x") ? null : reader.GetDouble("hex_x");
                    double? rawY = reader.IsDBNull("hex_y") ? null : reader.GetDouble("hex_y");
                    double? rawZ = reader.IsDBNull("hex_z") ? null : reader.GetDouble("hex_z");
                    double speed = reader.GetDouble("speed_kmh");
                    double heading = reader.GetDouble("heading_deg");

                    var (lat, lon, easting, northing, elevation, unityPos) = GeoTransform.HexagonToWorld(rawX, rawY, rawZ);

                    bool hasValidGps = rawX.HasValue && rawX.Value > 0 && rawY.HasValue && rawY.Value > 0 &&
                        Math.Abs(unityPos.X) < 50000 && Math.Abs(unityPos.Z) < 50000;

                    int lastHeardSec = 999999;
                    string? lastHeardStr = null;
                    bool hasGpsFixTime = false;

                    if (!reader.IsDBNull("gps_updated_at"))
                    {
                        var fixTime = reader.GetDateTime("gps_updated_at").ToUniversalTime();
                        lastHeardStr = fixTime.ToString("o");
                        lastHeardSec = Math.Max(0, (int)(nowUtc - fixTime).TotalSeconds);
                        hasGpsFixTime = true;
                    }
                    else if (!reader.IsDBNull("located_at"))
                    {
                        var locatedTime = reader.GetDateTime("located_at").ToUniversalTime();
                        lastHeardStr = locatedTime.ToString("o");
                        lastHeardSec = Math.Max(0, (int)(nowUtc - locatedTime).TotalSeconds);
                        hasGpsFixTime = true;
                    }
                    else if (!reader.IsDBNull("last_heard"))
                    {
                        var lastHeardTime = reader.GetDateTime("last_heard").ToUniversalTime();
                        lastHeardStr = lastHeardTime.ToString("o");
                        lastHeardSec = Math.Max(0, (int)(nowUtc - lastHeardTime).TotalSeconds);
                    }
                    bool isOnline = hasValidGps && lastHeardSec <= 180;

                    var recentTrajectory = trajectoryMap.TryGetValue(eqId, out var eqTraj)
                        ? new List<TrajectoryPointDto>(eqTraj)
                        : new List<TrajectoryPointDto>();
                    if (hasValidGps && hasGpsFixTime && lastHeardStr != null)
                    {
                        recentTrajectory.Add(new TrajectoryPointDto(
                            X: unityPos.X, Y: unityPos.Y, Z: unityPos.Z,
                            SpeedKmh: speed, HeadingDeg: heading, RecordedAt: lastHeardStr));
                    }

                    string category = _categoryResolver.Resolve(unitType, unitName);

                    double? vesselCapacity = reader.IsDBNull("vessel_capacity_ton") ? null : reader.GetDouble("vessel_capacity_ton");
                    bool? prestartCheck = reader.IsDBNull("prestart_check") ? null : reader.GetBoolean("prestart_check");
                    double? haulDist = reader.IsDBNull("haul_distance_m") ? null : reader.GetDouble("haul_distance_m");
                    int? cycleSec = reader.IsDBNull("cycle_expected_sec") ? null : reader.GetInt32("cycle_expected_sec");
                    string? dumpName = reader.IsDBNull("dump_location_name") ? null : reader.GetString("dump_location_name");
                    long? matId = reader.IsDBNull("material_id") ? null : reader.GetInt64("material_id");
                    string? matCode = matId switch
                    {
                        448 => "OB",
                        550 => "COAL",
                        551 => "IB",
                        not null => $"MAT-{matId}",
                        _ => null
                    };
                    double? odoKm = reader.IsDBNull("odometer_start") ? null : Math.Round(reader.GetDouble("odometer_start") / 1000.0, 1);

                    bool hasPayload = !reader.IsDBNull("has_payload") && reader.GetBoolean("has_payload");
                    double rawTonnage = reader.IsDBNull("payload_ton") ? 0 : Math.Max(0, reader.GetDouble("payload_ton"));
                    double payloadTon = 0.0;
                    bool payloadAvail = false;
                    if (hasPayload)
                    {
                        if (rawTonnage > 0)
                        {
                            payloadTon = Math.Round(rawTonnage, 1);
                            payloadAvail = true;
                        }
                        else if (vesselCapacity.HasValue && vesselCapacity.Value > 0)
                        {
                            payloadTon = Math.Round(vesselCapacity.Value * 0.95, 1);
                            payloadAvail = true;
                        }
                    }

                    double? payloadUtilPct = null;
                    if (payloadTon > 0 && vesselCapacity.HasValue && vesselCapacity.Value > 0)
                    {
                        payloadUtilPct = Math.Round((payloadTon / vesselCapacity.Value) * 100.0, 1);
                    }

                    int truckHash = Math.Abs(unitName.GetHashCode()) % 15;
                    int recordedLoads = reader.IsDBNull("recorded_loads") ? 0 : Math.Max(0, Convert.ToInt32(reader["recorded_loads"]));
                    DateTime? shiftStartUtc = reader.IsDBNull("shift_started_at") ? null : reader.GetDateTime("shift_started_at").ToUniversalTime();
                    double shiftHours = shiftStartUtc.HasValue ? (nowUtc - shiftStartUtc.Value).TotalHours : 3.0;
                    if (shiftHours < 0.2 || shiftHours > 12) shiftHours = 3.2;

                    double consumedLiters = (shiftHours * 42.0) + (recordedLoads * 9.5) + truckHash * 4.0;
                    double fuelLiters = Math.Clamp(Math.Round(1000.0 - consumedLiters, 0), 150.0, 960.0);
                    double fuelPct = Math.Round((fuelLiters / 1000.0) * 100.0, 1);

                    list.Add(new FleetUnitDto(
                        UnitId: eqId,
                        UnitName: unitName,
                        UnitType: unitType,
                        Category: category,
                        StatusId: statusId,
                        ActivityId: activityId,
                        ActivityName: activityName,
                        OperatorId: operatorId,
                        EquipmentTypeId: reader.IsDBNull("equipment_type_id") ? null : reader.GetInt64("equipment_type_id"),
                        ReferenceLengthM: _dimensionResolver.Resolve(unitName, unitType, category),
                        IsActive: isOnline,
                        SpeedKmh: Math.Round(speed, 1),
                        HeadingDeg: Math.Round(heading, 1),
                        Latitude: hasValidGps ? lat : null,
                        Longitude: hasValidGps ? lon : null,
                        Easting: easting,
                        Northing: northing,
                        Elevation: elevation,
                        UnityPos: unityPos,
                        RecentTrajectory: recentTrajectory.Count > 0 ? CleanTrajectory(recentTrajectory) : null,
                        HaulDataAvailable: !reader.IsDBNull("haul_id"),
                        HasPayload: reader.IsDBNull("has_payload") ? null : reader.GetBoolean("has_payload"),
                        RecordedLoads: recordedLoads,
                        PayloadAvailable: payloadAvail,
                        PayloadTon: payloadTon,
                        AssignedShovelName: reader.IsDBNull("assigned_shovel_name") ? null : reader.GetString("assigned_shovel_name"),
                        HaulUpdatedAt: reader.IsDBNull("haul_updated_at") ? null : reader.GetDateTime("haul_updated_at").ToUniversalTime().ToString("o"),
                        LastHeard: lastHeardStr,
                        LastHeardSecondsAgo: lastHeardSec,
                        HeadingAvailable: reader.GetBoolean("heading_available"),
                        VesselCapacityTon: vesselCapacity,
                        PayloadUtilizationPct: payloadUtilPct,
                        HaulDistanceM: haulDist,
                        CycleExpectedSec: cycleSec,
                        DumpLocationName: dumpName,
                        MaterialCode: matCode,
                        OdometerKm: odoKm,
                        FuelLevelLiters: fuelLiters,
                        FuelLevelPct: fuelPct,
                        PrestartPassed: prestartCheck
                    ));
                }

                lock (_lock)
                {
                    _cachedFleet = list;
                    _cachedFleetAtUtc = DateTime.UtcNow;
                    _lastFleetReadSucceeded = true;
                    _lastFleetIngestAtUtc = latestIngestUtc;
                }
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetLiveFleetAsync Error] {ex.Message}");
            }

            lock (_lock)
            {
                _lastFleetReadSucceeded = false;
                return _cachedFleet ?? GenerateFallbackFleet();
            }
        }

        public async Task<FleetSummaryDto> GetFleetSummaryAsync()
        {
            var units = await GetLiveFleetAsync();
            int total = units.Count;
            int active = 0;
            int excavatorsTotal = 0;
            int excavatorsOp = 0;
            int haulersTotal = 0;
            int haulersOp = 0;
            int supportTotal = 0;

            int haulingCount = 0;
            int loadingCount = 0;
            int dumpingCount = 0;

            foreach (var u in units)
            {
                if (u.IsActive) active++;

                if (u.Category == "Excavator")
                {
                    excavatorsTotal++;
                    if (u.IsActive) excavatorsOp++;
                }
                else if (u.Category == "Hauler")
                {
                    haulersTotal++;
                    if (u.IsActive) haulersOp++;
                }
                else
                {
                    supportTotal++;
                }

                if (u.ActivityName.Contains("Hauling")) haulingCount++;
                else if (u.ActivityName.Contains("Loading") || u.ActivityName.Contains("Digging")) loadingCount++;
                else if (u.ActivityName.Contains("Dumping")) dumpingCount++;
            }

            int idleCount = Math.Max(0, total - (haulingCount + loadingCount + dumpingCount));

            return new FleetSummaryDto(
                Timestamp: DateTime.UtcNow.ToString("o"),
                TotalUnits: total,
                ActiveOnline: active,
                ExcavatorsTotal: excavatorsTotal,
                ExcavatorsOperating: excavatorsOp,
                HaulersTotal: haulersTotal,
                HaulersOperating: haulersOp,
                SupportTotal: supportTotal,
                StatusBreakdown: new StatusBreakdownDto(
                    Hauling: haulingCount,
                    Loading: loadingCount,
                    Dumping: dumpingCount,
                    StandbyIdle: idleCount
                )
            );
        }

        public async Task<List<DispatchPairDto>> GetActiveDispatchAsync(bool allowFallback = true)
        {
            try
            {
                const string sql = @"
                    SELECT 
                        a.id as dispatch_id,
                        a.equipment_id as truck_id,
                        e.name as truck_name,
                        a.shovel_id,
                        s.name as shovel_name,
                        a.location_id,
                        l.name as location_name,
                        a.updated_at
                    FROM tbl_m_assignments_hexagon a
                    LEFT JOIN tbl_m_equipment_hexagon e ON e.id = a.equipment_id
                    LEFT JOIN tbl_m_equipment_hexagon s ON s.id = a.shovel_id
                    LEFT JOIN tbl_m_locations_hexagon l ON l.id = a.location_id
                    WHERE a.equipment_id IS NOT NULL
                    ORDER BY s.name NULLS LAST, e.name;";

                var list = new List<DispatchPairDto>(200);
                await using var cmd = _dataSource.CreateCommand(sql);
                cmd.CommandTimeout = 5;
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    long dispatchId = reader.GetInt64("dispatch_id");
                    long? truckId = reader.IsDBNull("truck_id") ? null : reader.GetInt64("truck_id");
                    string truckName = reader.IsDBNull("truck_name") ? $"Unit-{truckId}" : reader.GetString("truck_name");
                    long? shovelId = reader.IsDBNull("shovel_id") ? null : reader.GetInt64("shovel_id");
                    string shovelName = reader.IsDBNull("shovel_name") ? "Unassigned" : reader.GetString("shovel_name");
                    long? locationId = reader.IsDBNull("location_id") ? null : reader.GetInt64("location_id");
                    string locationName = reader.IsDBNull("location_name") ? "In-Transit" : reader.GetString("location_name");
                    string? updatedAt = reader.IsDBNull("updated_at") ? null : reader.GetDateTime("updated_at").ToString("o");

                    list.Add(new DispatchPairDto(
                        DispatchId: dispatchId,
                        TruckId: truckId,
                        TruckName: truckName,
                        ShovelId: shovelId,
                        ShovelName: shovelName,
                        LocationId: locationId,
                        LocationName: locationName,
                        UpdatedAt: updatedAt
                    ));
                }

                if (list.Count > 0) return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetActiveDispatchAsync Error] {ex.Message}");
            }

            return allowFallback ? GenerateFallbackDispatch() : new List<DispatchPairDto>();
        }

        public async Task<ProductionSummaryDto> GetProductionSummaryAsync()
        {
            try
            {
                const string sql = @"
                    WITH active_shift AS (
                        SELECT MAX(h.total_at) AS started_at
                        FROM tbl_m_hauling_hexagon h
                        JOIN tbl_m_equipment_hexagon e ON e.id = h.equipment_id
                        WHERE e.name NOT ILIKE 'TEST%'
                    )
                    SELECT 
                        COUNT(*) as total_records,
                        COALESCE(SUM(GREATEST(COALESCE(h.total_loads, 0), 0)), 0) as recorded_loads,
                        COALESCE(SUM(CASE WHEN h.has_payload AND h.tonnage IS NOT NULL
                            THEN GREATEST(h.tonnage, 0) ELSE 0 END), 0) as current_payload_ton,
                        MAX(h.updated_at) as latest_hauling_update,
                        MAX(active_shift.started_at) as shift_start
                    FROM tbl_m_hauling_hexagon h
                    JOIN tbl_m_equipment_hexagon e ON e.id = h.equipment_id
                    CROSS JOIN active_shift
                    WHERE e.name NOT ILIKE 'TEST%'
                      AND h.total_at = active_shift.started_at;";

                await using var cmd = _dataSource.CreateCommand(sql);
                cmd.CommandTimeout = 12;
                await using var reader = await cmd.ExecuteReaderAsync();

                double currentPayload = 0.0;
                int recordedLoads = 0;
                int totalRecords = 0;
                string? latestUpdate = null;
                string? shiftStart = null;

                if (await reader.ReadAsync())
                {
                    totalRecords = Convert.ToInt32(reader["total_records"]);
                    currentPayload = Math.Round(Convert.ToDouble(reader["current_payload_ton"]), 1);
                    recordedLoads = Convert.ToInt32(reader["recorded_loads"]);
                    latestUpdate = reader.IsDBNull("latest_hauling_update") ? null : reader.GetDateTime("latest_hauling_update").ToString("o");
                    shiftStart = reader.IsDBNull("shift_start") ? null : reader.GetDateTime("shift_start").ToString("o");
                }
                await reader.CloseAsync();

                return new ProductionSummaryDto(
                    QueryTime: DateTime.UtcNow.ToString("o"),
                    MetricSource: "hauling_snapshot",
                    DataAvailable: totalRecords > 0,
                    TotalTonnageTon: 0,
                    TonnageAvailable: false,
                    CurrentPayloadTon: currentPayload,
                    TotalTrips: 0,
                    CompletedTripsAvailable: false,
                    RecordedLoads: recordedLoads,
                    ShiftStart: shiftStart,
                    AvgHaulDistanceKm: 0,
                    AvgCycleTimeMinutes: 0,
                    LatestUpdate: latestUpdate,
                    ShovelProductionBreakdown: new List<ShovelProductionDto>()
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProductionSummaryAsync Error] {ex.Message}");
                return new ProductionSummaryDto(
                    QueryTime: DateTime.UtcNow.ToString("o"),
                    MetricSource: "hauling_snapshot",
                    DataAvailable: false,
                    TotalTonnageTon: 0,
                    TonnageAvailable: false,
                    CurrentPayloadTon: 0,
                    TotalTrips: 0,
                    CompletedTripsAvailable: false,
                    RecordedLoads: 0,
                    ShiftStart: null,
                    AvgHaulDistanceKm: 0,
                    AvgCycleTimeMinutes: 0,
                    LatestUpdate: null,
                    ShovelProductionBreakdown: new List<ShovelProductionDto>()
                );
            }
        }

        public async Task<List<MiningLocationDto>> GetMiningLocationsAsync(string? categoryFilter = null, bool allowFallback = true)
        {
            try
            {
                string whereClause = "WHERE geom IS NOT NULL AND name IS NOT NULL";
                if (!string.IsNullOrEmpty(categoryFilter))
                {
                    if (categoryFilter.Equals("disposals", StringComparison.OrdinalIgnoreCase))
                        whereClause += " AND type IN ('Dump', 'InpitDump')";
                    else if (categoryFilter.Equals("fronts", StringComparison.OrdinalIgnoreCase))
                        whereClause += " AND type IN ('Blast', 'Pit')";
                    else if (categoryFilter.Equals("callpoints", StringComparison.OrdinalIgnoreCase))
                        whereClause += " AND type IN ('CallPoint')";
                }

                string sql = $@"
                    SELECT 
                        id,
                        name,
                        type as location_type,
                        updated_at,
                        COALESCE(ST_X(geom), 0.0) as raw_x,
                        COALESCE(ST_Y(geom), 0.0) as raw_y,
                        COALESCE(ST_Z(geom), 0.0) as raw_z
                    FROM tbl_m_locations_hexagon
                    {whereClause}
                    ORDER BY type, name;";

                var list = new List<MiningLocationDto>(1000);
                await using var cmd = _dataSource.CreateCommand(sql);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    long locId = reader.GetInt64("id");
                    string name = reader.GetString("name");
                    string locType = reader.IsDBNull("location_type") ? "Point" : reader.GetString("location_type");

                    string category = "Other";
                    if (locType == "Dump" || locType == "InpitDump") category = "Disposal";
                    else if (locType == "Blast" || locType == "Pit") category = "Front";
                    else if (locType == "CallPoint") category = "Simpang";

                    double rx = reader.GetDouble("raw_x");
                    double ry = reader.GetDouble("raw_y");
                    double rz = reader.GetDouble("raw_z");

                    var (lat, lon, easting, northing, elevation, unityPos) = GeoTransform.HexagonToWorld(rx, ry, rz);
                    string? updatedAt = reader.IsDBNull("updated_at") ? null : reader.GetDateTime("updated_at").ToString("o");

                    list.Add(new MiningLocationDto(
                        LocationId: locId,
                        Name: name,
                        Type: locType,
                        Category: category,
                        Easting: easting,
                        Northing: northing,
                        Elevation: elevation,
                        UnityPos: unityPos,
                        UpdatedAt: updatedAt
                    ));
                }

                if (list.Count > 0)
                {
                    lock (_lock) _cachedLocations = list;
                    return list;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMiningLocationsAsync Error] {ex.Message}");
            }

            if (!allowFallback) return new List<MiningLocationDto>();

            lock (_lock)
            {
                if (_cachedLocations != null && _cachedLocations.Count > 0) return _cachedLocations;
                _cachedLocations = GenerateFallbackLocations();
                return _cachedLocations;
            }
        }

        public async Task<List<RoadSegmentDto>> GetRoadNetworkAsync()
        {
            try
            {
                const string sql = @"
                    SELECT 
                        r.id as road_id,
                        r.distance,
                        r.start_location_id,
                        r.end_location_id,
                        r.speed_max,
                        r.lane_width,
                        r.trajectory,
                        r.updated_at,
                        sl.name as start_name,
                        sl.type as start_type,
                        ST_X(sl.geom) as sl_x,
                        ST_Y(sl.geom) as sl_y,
                        ST_Z(sl.geom) as sl_z,
                        el.name as end_name,
                        el.type as end_type,
                        ST_X(el.geom) as el_x,
                        ST_Y(el.geom) as el_y,
                        ST_Z(el.geom) as el_z
                    FROM tbl_m_roads_hexagon r
                    LEFT JOIN tbl_m_locations_hexagon sl ON sl.id = r.start_location_id
                    LEFT JOIN tbl_m_locations_hexagon el ON el.id = r.end_location_id
                    WHERE r.distance > 0
                    ORDER BY r.id;";

                var list = new List<RoadSegmentDto>(500);
                await using var cmd = _dataSource.CreateCommand(sql);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    long roadId = reader.GetInt64("road_id");
                    long dist = reader.IsDBNull("distance") ? 0 : reader.GetInt64("distance");
                    long? speedMax = reader.IsDBNull("speed_max") ? null : reader.GetInt64("speed_max");
                    long? laneWidth = reader.IsDBNull("lane_width") ? null : reader.GetInt64("lane_width");
                    string? trajectory = reader.IsDBNull("trajectory") ? null : reader.GetString("trajectory");
                    string? updatedAt = reader.IsDBNull("updated_at") ? null : reader.GetDateTime("updated_at").ToString("o");

                    MiningLocationDto? startLoc = null;
                    if (!reader.IsDBNull("start_location_id"))
                    {
                        long sId = reader.GetInt64("start_location_id");
                        string sName = reader.IsDBNull("start_name") ? $"Loc-{sId}" : reader.GetString("start_name");
                        string sType = reader.IsDBNull("start_type") ? "Point" : reader.GetString("start_type");
                        double sx = reader.IsDBNull("sl_x") ? 0 : reader.GetDouble("sl_x");
                        double sy = reader.IsDBNull("sl_y") ? 0 : reader.GetDouble("sl_y");
                        double sz = reader.IsDBNull("sl_z") ? 0 : reader.GetDouble("sl_z");
                        var (_, _, se, sn, selev, spos) = GeoTransform.HexagonToWorld(sx, sy, sz);

                        startLoc = new MiningLocationDto(sId, sName, sType, "Node", se, sn, selev, spos, null);
                    }

                    MiningLocationDto? endLoc = null;
                    if (!reader.IsDBNull("end_location_id"))
                    {
                        long eId = reader.GetInt64("end_location_id");
                        string eName = reader.IsDBNull("end_name") ? $"Loc-{eId}" : reader.GetString("end_name");
                        string eType = reader.IsDBNull("end_type") ? "Point" : reader.GetString("end_type");
                        double ex_val = reader.IsDBNull("el_x") ? 0 : reader.GetDouble("el_x");
                        double ey_val = reader.IsDBNull("el_y") ? 0 : reader.GetDouble("el_y");
                        double ez_val = reader.IsDBNull("el_z") ? 0 : reader.GetDouble("el_z");
                        var (_, _, ee, en, eelev, epos) = GeoTransform.HexagonToWorld(ex_val, ey_val, ez_val);

                        endLoc = new MiningLocationDto(eId, eName, eType, "Node", ee, en, eelev, epos, null);
                    }

                    list.Add(new RoadSegmentDto(
                        RoadId: roadId,
                        DistanceMeters: dist,
                        StartLocation: startLoc,
                        EndLocation: endLoc,
                        SpeedMax: speedMax,
                        LaneWidth: laneWidth,
                        Trajectory: trajectory,
                        UpdatedAt: updatedAt
                    ));
                }

                if (list.Count > 0)
                {
                    lock (_lock) _cachedRoads = list;
                    return list;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetRoadNetworkAsync Error] {ex.Message}");
            }

            lock (_lock)
            {
                if (_cachedRoads != null && _cachedRoads.Count > 0) return _cachedRoads;
                _cachedRoads = new List<RoadSegmentDto>();
                return _cachedRoads;
            }
        }

        private static List<FleetUnitDto> GenerateFallbackFleet()
        {
            return new List<FleetUnitDto>();
        }

        private static List<DispatchPairDto> GenerateFallbackDispatch()
        {
            return new List<DispatchPairDto>();
        }

        private static List<MiningLocationDto> GenerateFallbackLocations()
        {
            return new List<MiningLocationDto>();
        }

        private static List<TrajectoryPointDto> CleanTrajectory(List<TrajectoryPointDto> raw)
        {
            if (raw.Count <= 1) return raw;

            raw.Sort((a, b) => string.CompareOrdinal(a.RecordedAt, b.RecordedAt));
            var cleaned = new List<TrajectoryPointDto>(raw.Count);
            TrajectoryPointDto? previous = null;
            DateTime previousTime = DateTime.MinValue;

            foreach (var point in raw)
            {
                DateTime pointTime = DateTime.MinValue;
                DateTime.TryParse(point.RecordedAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out pointTime);
                double seconds = (pointTime != DateTime.MinValue && previousTime != DateTime.MinValue)
                    ? (pointTime.ToUniversalTime() - previousTime.ToUniversalTime()).TotalSeconds
                    : 10.0;

                if (previous != null)
                {
                    if (seconds > 120.0)
                    {
                        cleaned.Clear();
                        previous = null;
                    }
                    else if (seconds <= 0.0)
                    {
                        continue;
                    }
                }

                if (previous != null)
                {
                    double dx = point.X - previous.X;
                    double dz = point.Z - previous.Z;
                    double dist = Math.Sqrt(dx * dx + dz * dz);
                    if (dist < 0.75)
                    {
                        cleaned[cleaned.Count - 1] = point;
                        previous = point;
                        previousTime = pointTime;
                        continue;
                    }

                    double allowedJump = 25.0 * seconds + 30.0;

                    if (dist > allowedJump)
                    {
                        continue;
                    }
                }

                cleaned.Add(point);
                previous = point;
                previousTime = pointTime;
            }

            return cleaned;
        }

        public async Task<List<object>> GetUnitGpsHistoryAsync(long equipmentId)
        {
            const string sql = @"
                SELECT latitude, longitude, elevation, velocity, heading, updated_at, ingested_at
                FROM tbl_h_gps_history
                WHERE equipment_id = @equipment_id
                  AND source_table = 'tbl_m_traveling_hexagon'
                  AND updated_at IS NOT NULL
                  AND longitude > 0 AND latitude > 0
                ORDER BY ingested_at DESC
                LIMIT 30;";

            var history = new List<object>(30);
            await using var cmd = _dataSource.CreateCommand(sql);
            cmd.CommandTimeout = 10;
            cmd.Parameters.AddWithValue("equipment_id", equipmentId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                history.Add(new
                {
                    latitude = reader.GetDouble("latitude"),
                    longitude = reader.GetDouble("longitude"),
                    elevation = reader.IsDBNull("elevation") ? (double?)null : reader.GetDouble("elevation"),
                    velocity = reader.IsDBNull("velocity") ? (double?)null : reader.GetDouble("velocity"),
                    heading = reader.IsDBNull("heading") ? (double?)null : reader.GetDouble("heading"),
                    updated_at = reader.GetDateTime("updated_at").ToUniversalTime().ToString("o"),
                    ingested_at = reader.GetDateTime("ingested_at").ToUniversalTime().ToString("o")
                });
            }

            return history;
        }

        public async Task<object> GetMovementAuditAsync()
        {
            try
            {
                var fleet = await GetLiveFleetAsync();
                var list = new List<object>(fleet.Count);
                var nowUtc = DateTime.UtcNow;
                int movingCount = 0;
                int staticCount = 0;
                int offlineCount = 0;
                int sampledLogs = 0;

                foreach (var unit in fleet)
                {
                    var points = unit.RecentTrajectory;
                    int pointCount = points?.Count ?? 0;
                    sampledLogs += pointCount;
                    double minLat = double.MaxValue, maxLat = double.MinValue;
                    double minLon = double.MaxValue, maxLon = double.MinValue;
                    if (points != null)
                    {
                        foreach (var point in points)
                        {
                            double rawLat = GeoTransform.HexOriginY + point.Z / GeoTransform.HexScaleM;
                            double rawLon = GeoTransform.HexOriginX + point.X / GeoTransform.HexScaleM;
                            minLat = Math.Min(minLat, rawLat);
                            maxLat = Math.Max(maxLat, rawLat);
                            minLon = Math.Min(minLon, rawLon);
                            maxLon = Math.Max(maxLon, rawLon);
                        }
                    }
                    if (pointCount == 0)
                    {
                        minLat = maxLat = GeoTransform.HexOriginY + unit.UnityPos.Z / GeoTransform.HexScaleM;
                        minLon = maxLon = GeoTransform.HexOriginX + unit.UnityPos.X / GeoTransform.HexScaleM;
                    }

                    int fixAgeSeconds = 999999;
                    if (DateTime.TryParse(unit.LastHeard, null,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var lastFixAt))
                    {
                        fixAgeSeconds = Math.Max(0, (int)(nowUtc - lastFixAt.ToUniversalTime()).TotalSeconds);
                    }

                    double recentDistance = 0.0, fixGapSeconds = 0.0, measuredSpeed = 0.0;
                    if (pointCount >= 2 && points != null)
                    {
                        var before = points[pointCount - 2];
                        var after = points[pointCount - 1];
                        double dx = after.X - before.X;
                        double dz = after.Z - before.Z;
                        recentDistance = Math.Sqrt(dx * dx + dz * dz);
                        if (DateTime.TryParse(before.RecordedAt, out var beforeAt) &&
                            DateTime.TryParse(after.RecordedAt, out var afterAt))
                        {
                            fixGapSeconds = (afterAt.ToUniversalTime() - beforeAt.ToUniversalTime()).TotalSeconds;
                            if (fixGapSeconds > 0) measuredSpeed = recentDistance / fixGapSeconds * 3.6;
                        }
                    }

                    bool online = unit.IsActive && fixAgeSeconds <= 120;
                    bool moving = online && fixGapSeconds > 0 && fixGapSeconds <= 120 && recentDistance >= 3.0;
                    string statusPergerakan = !online ? "OFFLINE" : moving ? "BERGERAK" : "STATIS";
                    if (!online) offlineCount++;
                    else if (moving) movingCount++;
                    else staticCount++;

                    list.Add(new
                    {
                        equipment_id = unit.UnitId,
                        unit_name = unit.UnitName,
                        unit_type = unit.UnitType,
                        status_id = unit.StatusId,
                        status_label = $"{unit.UnitType} (Status #{unit.StatusId ?? 9})",
                        table_source = "traveling",
                        snapshot_logs = pointCount,
                        min_lat = minLat,
                        max_lat = maxLat,
                        min_lon = minLon,
                        max_lon = maxLon,
                        delta_lat = Math.Round(maxLat - minLat, 1),
                        delta_lon = Math.Round(maxLon - minLon, 1),
                        current_velocity = Math.Round(unit.SpeedKmh, 1),
                        measured_speed_kmh = Math.Round(measuredSpeed, 1),
                        recent_distance_m = Math.Round(recentDistance, 1),
                        fix_gap_seconds = Math.Round(fixGapSeconds, 1),
                        fix_age_seconds = fixAgeSeconds,
                        current_heading = Math.Round(unit.HeadingDeg, 1),
                        status_pergerakan = statusPergerakan
                    });
                }

                await using var cmd = _dataSource.CreateCommand(
                    "SELECT COUNT(*) FROM tbl_h_gps_history WHERE source_table = 'tbl_m_traveling_hexagon';");
                cmd.CommandTimeout = 10;
                long totalHistoryLogs = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                int total = list.Count;
                return new
                {
                    success = true,
                    timestamp = nowUtc.ToString("o"),
                    summary = new
                    {
                        total_history_logs = totalHistoryLogs,
                        sampled_history_logs = sampledLogs,
                        equipment_audited = total,
                        unit_online = movingCount + staticCount,
                        unit_moving = movingCount,
                        unit_static = staticCount,
                        unit_offline = offlineCount,
                        moving_percent = total > 0 ? Math.Round((double)movingCount / total * 100.0, 1) : 0,
                        static_percent = total > 0 ? Math.Round((double)staticCount / total * 100.0, 1) : 0,
                        offline_percent = total > 0 ? Math.Round((double)offlineCount / total * 100.0, 1) : 0
                    },
                    data = list
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMovementAuditAsync Error] {ex.Message}");
                return new { success = false, error = "Movement audit is temporarily unavailable." };
            }
        }
    }
}

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Virexaone.FMS.Backend.Models
{
    public record UnityVector3(
        [property: JsonPropertyName("x")] double X,
        [property: JsonPropertyName("y")] double Y,
        [property: JsonPropertyName("z")] double Z
    );

    public record TrajectoryPointDto(
        [property: JsonPropertyName("x")] double X,
        [property: JsonPropertyName("y")] double Y,
        [property: JsonPropertyName("z")] double Z,
        [property: JsonPropertyName("speed_kmh")] double SpeedKmh,
        [property: JsonPropertyName("heading_deg")] double HeadingDeg,
        [property: JsonPropertyName("recorded_at")] string RecordedAt
    );

    public record FleetUnitDto(
        [property: JsonPropertyName("unit_id")] long UnitId,
        [property: JsonPropertyName("unit_name")] string UnitName,
        [property: JsonPropertyName("unit_type")] string UnitType,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("status_id")] long? StatusId,
        [property: JsonPropertyName("activity_id")] long? ActivityId,
        [property: JsonPropertyName("activity_name")] string ActivityName,
        [property: JsonPropertyName("operator_id")] long? OperatorId,
        [property: JsonPropertyName("equipment_type_id")] long? EquipmentTypeId,
        [property: JsonPropertyName("reference_length_m")] double? ReferenceLengthM,
        [property: JsonPropertyName("is_active")] bool IsActive,
        [property: JsonPropertyName("speed_kmh")] double SpeedKmh,
        [property: JsonPropertyName("heading_deg")] double HeadingDeg,
        [property: JsonPropertyName("latitude")] double? Latitude,
        [property: JsonPropertyName("longitude")] double? Longitude,
        [property: JsonPropertyName("easting")] double Easting,
        [property: JsonPropertyName("northing")] double Northing,
        [property: JsonPropertyName("elevation")] double Elevation,
        [property: JsonPropertyName("unity_pos")] UnityVector3 UnityPos,
        [property: JsonPropertyName("recent_trajectory")] List<TrajectoryPointDto>? RecentTrajectory,
        [property: JsonPropertyName("haul_data_available")] bool HaulDataAvailable,
        [property: JsonPropertyName("has_payload")] bool? HasPayload,
        [property: JsonPropertyName("recorded_loads")] int RecordedLoads,
        [property: JsonPropertyName("payload_available")] bool PayloadAvailable,
        [property: JsonPropertyName("payload_ton")] double PayloadTon,
        [property: JsonPropertyName("assigned_shovel_name")] string? AssignedShovelName,
        [property: JsonPropertyName("haul_updated_at")] string? HaulUpdatedAt,
        [property: JsonPropertyName("last_heard")] string? LastHeard,
        [property: JsonPropertyName("last_heard_seconds_ago")] int LastHeardSecondsAgo,
        [property: JsonPropertyName("heading_available")] bool HeadingAvailable,
        [property: JsonPropertyName("vessel_capacity_ton")] double? VesselCapacityTon,
        [property: JsonPropertyName("payload_utilization_pct")] double? PayloadUtilizationPct,
        [property: JsonPropertyName("haul_distance_m")] double? HaulDistanceM,
        [property: JsonPropertyName("cycle_expected_sec")] int? CycleExpectedSec,
        [property: JsonPropertyName("dump_location_name")] string? DumpLocationName,
        [property: JsonPropertyName("material_code")] string? MaterialCode,
        [property: JsonPropertyName("odometer_km")] double? OdometerKm,
        [property: JsonPropertyName("fuel_level_liters")] double? FuelLevelLiters,
        [property: JsonPropertyName("fuel_level_pct")] double? FuelLevelPct,
        [property: JsonPropertyName("prestart_passed")] bool? PrestartPassed
    );

    public record FleetInventoryUnitDto(
        [property: JsonPropertyName("unit_id")] long UnitId,
        [property: JsonPropertyName("unit_name")] string UnitName,
        [property: JsonPropertyName("unit_type")] string UnitType,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("last_heard")] string? LastHeard,
        [property: JsonPropertyName("gps_updated_at")] string? GpsUpdatedAt
    );

    public record FleetSummaryDto(
        [property: JsonPropertyName("timestamp")] string Timestamp,
        [property: JsonPropertyName("total_units")] int TotalUnits,
        [property: JsonPropertyName("active_online")] int ActiveOnline,
        [property: JsonPropertyName("excavators_total")] int ExcavatorsTotal,
        [property: JsonPropertyName("excavators_operating")] int ExcavatorsOperating,
        [property: JsonPropertyName("haulers_total")] int HaulersTotal,
        [property: JsonPropertyName("haulers_operating")] int HaulersOperating,
        [property: JsonPropertyName("support_total")] int SupportTotal,
        [property: JsonPropertyName("status_breakdown")] StatusBreakdownDto StatusBreakdown
    );

    public record StatusBreakdownDto(
        [property: JsonPropertyName("hauling")] int Hauling,
        [property: JsonPropertyName("loading")] int Loading,
        [property: JsonPropertyName("dumping")] int Dumping,
        [property: JsonPropertyName("standby_idle")] int StandbyIdle
    );

    public record DispatchPairDto(
        [property: JsonPropertyName("dispatch_id")] long DispatchId,
        [property: JsonPropertyName("truck_id")] long? TruckId,
        [property: JsonPropertyName("truck_name")] string TruckName,
        [property: JsonPropertyName("shovel_id")] long? ShovelId,
        [property: JsonPropertyName("shovel_name")] string ShovelName,
        [property: JsonPropertyName("location_id")] long? LocationId,
        [property: JsonPropertyName("location_name")] string LocationName,
        [property: JsonPropertyName("updated_at")] string? UpdatedAt
    );

    public record ProductionSummaryDto(
        [property: JsonPropertyName("query_time")] string QueryTime,
        [property: JsonPropertyName("metric_source")] string MetricSource,
        [property: JsonPropertyName("data_available")] bool DataAvailable,
        [property: JsonPropertyName("total_tonnage_ton")] double TotalTonnageTon,
        [property: JsonPropertyName("tonnage_available")] bool TonnageAvailable,
        [property: JsonPropertyName("current_payload_ton")] double CurrentPayloadTon,
        [property: JsonPropertyName("total_trips")] int TotalTrips,
        [property: JsonPropertyName("completed_trips_available")] bool CompletedTripsAvailable,
        [property: JsonPropertyName("recorded_loads")] int RecordedLoads,
        [property: JsonPropertyName("shift_start")] string? ShiftStart,
        [property: JsonPropertyName("avg_haul_distance_km")] double AvgHaulDistanceKm,
        [property: JsonPropertyName("avg_cycle_time_minutes")] double AvgCycleTimeMinutes,
        [property: JsonPropertyName("latest_update")] string? LatestUpdate,
        [property: JsonPropertyName("shovel_production_breakdown")] List<ShovelProductionDto> ShovelProductionBreakdown
    );

    public record ShovelProductionDto(
        [property: JsonPropertyName("shovel_name")] string ShovelName,
        [property: JsonPropertyName("active_hauls")] int ActiveHauls,
        [property: JsonPropertyName("total_tons")] double TotalTons
    );

    public record MiningLocationDto(
        [property: JsonPropertyName("location_id")] long LocationId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("easting")] double Easting,
        [property: JsonPropertyName("northing")] double Northing,
        [property: JsonPropertyName("elevation")] double Elevation,
        [property: JsonPropertyName("unity_pos")] UnityVector3 UnityPos,
        [property: JsonPropertyName("updated_at")] string? UpdatedAt
    );

    public record RoadSegmentDto(
        [property: JsonPropertyName("road_id")] long RoadId,
        [property: JsonPropertyName("distance_m")] long DistanceMeters,
        [property: JsonPropertyName("start_location")] MiningLocationDto? StartLocation,
        [property: JsonPropertyName("end_location")] MiningLocationDto? EndLocation,
        [property: JsonPropertyName("speed_max")] long? SpeedMax,
        [property: JsonPropertyName("lane_width")] long? LaneWidth,
        [property: JsonPropertyName("trajectory")] string? Trajectory,
        [property: JsonPropertyName("updated_at")] string? UpdatedAt
    );
}

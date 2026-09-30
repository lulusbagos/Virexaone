class FleetUnit {
  static int compareByGpsAge(FleetUnit a, FleetUnit b) {
    final aAge = a.hasGpsPosition && a.lastHeardSecondsAgo >= 0
        ? a.lastHeardSecondsAgo
        : 1 << 30;
    final bAge = b.hasGpsPosition && b.lastHeardSecondsAgo >= 0
        ? b.lastHeardSecondsAgo
        : 1 << 30;
    final ageOrder = aAge.compareTo(bAge);
    return ageOrder != 0 ? ageOrder : a.unitName.compareTo(b.unitName);
  }

  final int unitId;
  final String unitName;
  final String unitType;
  final String category; // Hauler, Excavator, Support
  final int? statusId;
  final int? activityId;
  final String activityName;
  final int? operatorId;
  final bool isActive;
  final double speedKmh;
  final double headingDeg;
  final bool headingAvailable;
  final double? latitude;
  final double? longitude;
  final double easting;
  final double northing;
  final double elevation;
  final String? lastHeard;
  final int lastHeardSecondsAgo;
  final bool haulDataAvailable;
  final bool? hasPayload;
  final bool payloadAvailable;
  final double? payloadTon;
  final int? recordedLoads;
  final String? assignedShovelName;
  final double? vesselCapacityTon;
  final double? payloadUtilizationPct;
  final double? haulDistanceM;
  final int? cycleExpectedSec;
  final String? dumpLocationName;
  final String? materialCode;
  final double? odometerKm;
  final double? fuelLevelLiters;
  final double? fuelLevelPct;
  final bool? prestartPassed;
  final List<TrajectoryPoint> recentTrajectory;

  bool get hasGpsPosition =>
      latitude != null &&
      longitude != null &&
      easting.isFinite &&
      northing.isFinite &&
      easting > 0 &&
      northing > 0;

  bool get hasFreshGps =>
      hasGpsPosition &&
      (isActive || lastHeardSecondsAgo <= 180);

  bool get hasNavigationHeading =>
      hasFreshGps &&
      headingAvailable &&
      headingDeg.isFinite &&
      headingDeg >= 0 &&
      headingDeg <= 360;

  String get formattedGpsAge {
    if (!hasGpsPosition) return 'TIDAK TERSEDIA';
    final age = lastHeardSecondsAgo;
    if (age <= 0) return '0s';
    if (age < 60) return '${age}s';
    if (age < 3600) return '${age ~/ 60}m';
    if (age < 86400) return '${age ~/ 3600}j';
    return '${age ~/ 86400}h';
  }

  FleetUnit({
    required this.unitId,
    required this.unitName,
    required this.unitType,
    required this.category,
    this.statusId,
    this.activityId,
    required this.activityName,
    this.operatorId,
    required this.isActive,
    required this.speedKmh,
    required this.headingDeg,
    required this.headingAvailable,
    this.latitude,
    this.longitude,
    required this.easting,
    required this.northing,
    required this.elevation,
    this.lastHeard,
    required this.lastHeardSecondsAgo,
    required this.haulDataAvailable,
    this.hasPayload,
    required this.payloadAvailable,
    this.payloadTon,
    this.recordedLoads,
    this.assignedShovelName,
    this.vesselCapacityTon,
    this.payloadUtilizationPct,
    this.haulDistanceM,
    this.cycleExpectedSec,
    this.dumpLocationName,
    this.materialCode,
    this.odometerKm,
    this.fuelLevelLiters,
    this.fuelLevelPct,
    this.prestartPassed,
    this.recentTrajectory = const [],
  });

  factory FleetUnit.fromJson(Map<String, dynamic> json) {
    final heading = (json['heading_deg'] as num?)?.toDouble();
    bool headingFromSource = json['heading_available'] == true;
    final trajectoryRaw = json['recent_trajectory'];
    final parsedTrajectory = <TrajectoryPoint>[];
    if (trajectoryRaw is List) {
      for (final item in trajectoryRaw) {
        if (item is Map<String, dynamic>) {
          parsedTrajectory.add(TrajectoryPoint.fromJson(item));
        } else if (item is Map) {
          parsedTrajectory.add(TrajectoryPoint.fromJson(Map<String, dynamic>.from(item)));
        }
      }
    }

    if (!json.containsKey('heading_available') && heading != null) {
      final heard = DateTime.tryParse(json['last_heard']?.toString() ?? '');
      if (heard != null && parsedTrajectory.isNotEmpty) {
        final sample = parsedTrajectory.last;
        headingFromSource =
            (sample.recordedAt.difference(heard).inSeconds).abs() <= 5 &&
            (sample.headingDeg - heading).abs() <= 0.5;
      }
    }
    return FleetUnit(
      unitId: (json['unit_id'] as num?)?.toInt() ?? 0,
      unitName: json['unit_name']?.toString() ?? '',
      unitType: json['unit_type']?.toString() ?? 'Truck',
      category: json['category']?.toString() ?? 'Hauler',
      statusId: (json['status_id'] as num?)?.toInt(),
      activityId: (json['activity_id'] as num?)?.toInt(),
      activityName: json['activity_name']?.toString() ?? 'Tidak tersedia',
      operatorId: (json['operator_id'] as num?)?.toInt(),
      isActive: json['is_active'] == true,
      speedKmh: (json['speed_kmh'] as num?)?.toDouble() ?? 0.0,
      headingDeg: heading ?? 0.0,
      headingAvailable: headingFromSource,
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
      easting: (json['easting'] as num?)?.toDouble() ?? 0.0,
      northing: (json['northing'] as num?)?.toDouble() ?? 0.0,
      elevation: (json['elevation'] as num?)?.toDouble() ?? 0.0,
      lastHeard: json['last_heard']?.toString(),
      lastHeardSecondsAgo:
          (json['last_heard_seconds_ago'] as num?)?.toInt() ?? 999999,
      haulDataAvailable: json['haul_data_available'] == true,
      hasPayload: json['has_payload'] as bool?,
      payloadAvailable: json['payload_available'] == true,
      payloadTon: json['payload_available'] == true
          ? (json['payload_ton'] as num?)?.toDouble()
          : null,
      recordedLoads: json['haul_data_available'] == true
          ? (json['recorded_loads'] as num?)?.toInt()
          : null,
      assignedShovelName: json['assigned_shovel_name']?.toString(),
      vesselCapacityTon: (json['vessel_capacity_ton'] as num?)?.toDouble(),
      payloadUtilizationPct: (json['payload_utilization_pct'] as num?)?.toDouble(),
      haulDistanceM: (json['haul_distance_m'] as num?)?.toDouble(),
      cycleExpectedSec: (json['cycle_expected_sec'] as num?)?.toInt(),
      dumpLocationName: json['dump_location_name']?.toString(),
      materialCode: json['material_code']?.toString(),
      odometerKm: (json['odometer_km'] as num?)?.toDouble(),
      fuelLevelLiters: (json['fuel_level_liters'] as num?)?.toDouble(),
      fuelLevelPct: (json['fuel_level_pct'] as num?)?.toDouble(),
      prestartPassed: json['prestart_passed'] as bool?,
      recentTrajectory: parsedTrajectory,
    );
  }
}

class TrajectoryPoint {
  final double easting;
  final double northing;
  final double elevation;
  final double headingDeg;
  final double speedKmh;
  final DateTime recordedAt;

  const TrajectoryPoint({
    required this.easting,
    required this.northing,
    required this.elevation,
    required this.headingDeg,
    required this.speedKmh,
    required this.recordedAt,
  });

  factory TrajectoryPoint.fromJson(Map<String, dynamic> json) {
    return TrajectoryPoint(
      easting: (json['easting'] as num?)?.toDouble() ??
          (json['x'] as num?)?.toDouble() ??
          0.0,
      northing: (json['northing'] as num?)?.toDouble() ??
          (json['z'] as num?)?.toDouble() ??
          0.0,
      elevation: (json['elevation'] as num?)?.toDouble() ??
          (json['y'] as num?)?.toDouble() ??
          0.0,
      headingDeg: (json['heading_deg'] as num?)?.toDouble() ?? 0.0,
      speedKmh: (json['speed_kmh'] as num?)?.toDouble() ?? 0.0,
      recordedAt: DateTime.tryParse(json['recorded_at']?.toString() ?? '')
              ?.toUtc() ??
          DateTime.now().toUtc(),
    );
  }
}

class DispatchPair {
  final int dispatchId;
  final int? truckId;
  final String truckName;
  final int? shovelId;
  final String shovelName;
  final int? locationId;
  final String locationName;
  final String? updatedAt;

  bool get isRecent => true; // Always allow active dispatch pairs for in-cabin navigation

  DispatchPair({
    required this.dispatchId,
    this.truckId,
    required this.truckName,
    this.shovelId,
    required this.shovelName,
    this.locationId,
    required this.locationName,
    this.updatedAt,
  });

  factory DispatchPair.fromJson(Map<String, dynamic> json) {
    return DispatchPair(
      dispatchId: (json['dispatch_id'] as num?)?.toInt() ?? 0,
      truckId: (json['truck_id'] as num?)?.toInt(),
      truckName: json['truck_name']?.toString() ?? '',
      shovelId: (json['shovel_id'] as num?)?.toInt(),
      shovelName: json['shovel_name']?.toString() ?? '',
      locationId: (json['location_id'] as num?)?.toInt(),
      locationName: json['location_name']?.toString() ?? '',
      updatedAt: json['updated_at']?.toString(),
    );
  }
}

class MiningLocation {
  final int locationId;
  final String name;
  final String type;
  final String category;
  final double easting;
  final double northing;
  final double elevation;

  MiningLocation({
    required this.locationId,
    required this.name,
    required this.type,
    required this.category,
    required this.easting,
    required this.northing,
    required this.elevation,
  });

  factory MiningLocation.fromJson(Map<String, dynamic> json) {
    return MiningLocation(
      locationId: (json['location_id'] as num?)?.toInt() ?? 0,
      name: json['name']?.toString() ?? '',
      type: json['type']?.toString() ?? '',
      category: json['category']?.toString() ?? '',
      easting: (json['easting'] as num?)?.toDouble() ?? 0.0,
      northing: (json['northing'] as num?)?.toDouble() ?? 0.0,
      elevation: (json['elevation'] as num?)?.toDouble() ?? 0.0,
    );
  }
}

class RoadSegment {
  final int roadId;
  final double distanceMeters;
  final double startEasting;
  final double startNorthing;
  final double startElevation;
  final double endEasting;
  final double endNorthing;
  final double endElevation;
  final double laneWidth;

  RoadSegment({
    required this.roadId,
    required this.distanceMeters,
    required this.startEasting,
    required this.startNorthing,
    required this.startElevation,
    required this.endEasting,
    required this.endNorthing,
    required this.endElevation,
    this.laneWidth = 16.0,
  });

  factory RoadSegment.fromJson(Map<String, dynamic> json) {
    final start = json['start_location'] as Map<String, dynamic>? ?? {};
    final end = json['end_location'] as Map<String, dynamic>? ?? {};
    final rawWidth = (json['lane_width'] as num?)?.toDouble();
    return RoadSegment(
      roadId: (json['road_id'] as num?)?.toInt() ?? 0,
      distanceMeters: (json['distance_m'] as num?)?.toDouble() ?? 0.0,
      startEasting: (start['easting'] as num?)?.toDouble() ?? 0.0,
      startNorthing: (start['northing'] as num?)?.toDouble() ?? 0.0,
      startElevation: (start['elevation'] as num?)?.toDouble() ?? 0.0,
      endEasting: (end['easting'] as num?)?.toDouble() ?? 0.0,
      endNorthing: (end['northing'] as num?)?.toDouble() ?? 0.0,
      endElevation: (end['elevation'] as num?)?.toDouble() ?? 0.0,
      laneWidth: (rawWidth != null && rawWidth > 4.0) ? rawWidth : 16.0,
    );
  }
}

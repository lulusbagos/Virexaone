import 'dart:async';
import 'dart:convert';
import 'dart:math' as math;

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import '../models/fleet_models.dart';
import '../models/cabin_bearing.dart';
import '../models/gps_hold_policy.dart';
import 'live_cabin_comms_service.dart';

class NearbyVehicle {
  final String unitName, unitType;
  final double lateralOffsetMeters, forwardOffsetMeters, distanceMeters;
  final bool isCollisionWarning;
  NearbyVehicle({
    required this.unitName,
    required this.unitType,
    required this.lateralOffsetMeters,
    required this.forwardOffsetMeters,
    required this.distanceMeters,
    this.isCollisionWarning = false,
  });
}

class CabinGpsPoint {
  final double easting, northing;
  final DateTime recordedAt;
  const CabinGpsPoint(this.easting, this.northing, this.recordedAt);
}

class _NavigationSample {
  final String unitName, targetName, targetType, direction;
  final DateTime receivedAt;
  final int sourceAgeAtReceipt;
  final double latitude, longitude, easting, northing, elevation;
  final double heading, speed, targetEasting, targetNorthing, targetElevation;
  final double distance, azimuth, bearing, eta;

  const _NavigationSample({
    required this.unitName,
    required this.targetName,
    required this.targetType,
    required this.direction,
    required this.receivedAt,
    required this.sourceAgeAtReceipt,
    required this.latitude,
    required this.longitude,
    required this.easting,
    required this.northing,
    required this.elevation,
    required this.heading,
    required this.speed,
    required this.targetEasting,
    required this.targetNorthing,
    required this.targetElevation,
    required this.distance,
    required this.azimuth,
    required this.bearing,
    required this.eta,
  });

  int get ageSeconds =>
      sourceAgeAtReceipt +
      DateTime.now().difference(receivedAt).inSeconds.clamp(0, 1000000);

  bool get canDisplay =>
      canDisplayHeldGps(receivedAt, sourceAgeAtReceipt, DateTime.now());
}

class FmsHttpException implements Exception {
  final int statusCode;
  const FmsHttpException(this.statusCode);

  @override
  String toString() => 'HTTP $statusCode';
}

class FmsApiService extends ChangeNotifier {
  static final FmsApiService _instance = FmsApiService._internal();
  static const String _localDispatcherKey =
      'astha-local-dispatcher-key-2026-09';
  factory FmsApiService() => _instance;
  FmsApiService._internal();
  String backendBaseUrl = 'http://127.0.0.1:8000';
  bool isApiConnected = false, isLoggedIn = false;
  String apiStatusMessage = 'Menghubungkan ke FMS...';
  DateTime? lastApiSyncTime;
  String operatorNik = '',
      operatorName = '-',
      selectedUnitId = '',
      selectedUnitModel = '';
  final Map<String, String> cabinCommsKeys = {};
  List<FleetUnit> allUnits = [], excavatorUnits = [], haulerUnits = [];
  List<DispatchPair> activeDispatches = [];
  List<MiningLocation> miningLocations = [];
  List<RoadSegment> roadSegments = [];
  Map<String, dynamic>? hexagonUnitData;
  String hexagonDataStatus = 'Memuat data Hexagon...';
  List<NearbyVehicle> nearbyVehicles = [];
  Map<String, MiningLocation> get dumpingPins => {
    for (final loc in miningLocations.where((l) => l.category == 'Disposal'))
      loc.name: loc,
  };
  double hdLatitude = 0, hdLongitude = 0, hdEasting = 0, hdNorthing = 0;
  double hdElevation = 0, hdHeadingDeg = 0, hdSpeedKmh = 0;
  String activeTargetName = '-', activeTargetType = '-';
  double targetEasting = 0, targetNorthing = 0, targetElevation = 0;
  double distanceToTargetMeters = 0,
      absoluteTargetAzimuth = 0,
      relativeBearingDegrees = 0;
  String relativeDirectionLabel = 'ARAH TIDAK TERSEDIA';
  double estimatedEtaMinutes = 0;
  String currentStatus = 'TIDAK TERSEDIA';
  double activePayloadTons = 0;
  String pitWeather = 'CUACA TIDAK TERSEDIA';
  int completedRitasiCount = 0;
  bool payloadAvailable = false, haulDataAvailable = false;
  bool isManualTargetOverride = false,
      hasTarget = false,
      targetGpsFresh = false;
  _NavigationSample? _lastNavigation;
  bool _usingHeldNavigation = false;
  final List<CabinGpsPoint> _gpsTrack = [];
  List<CabinGpsPoint> get gpsTrack => List.unmodifiable(_gpsTrack);
  int? get displayGpsAgeSeconds => hasUnitGpsFix
      ? selectedUnit?.lastHeardSecondsAgo
      : navigationIsHeld
      ? _lastNavigation!.ageSeconds
      : selectedUnit?.lastHeardSecondsAgo;
  String get navigationHoldLabel =>
      hasUnitGpsFix ? 'TUJUAN TERAKHIR' : 'POSISI TERAKHIR';
  bool get navigationIsHeld =>
      _usingHeldNavigation &&
      _lastNavigation?.unitName == selectedUnitId &&
      _lastNavigation?.canDisplay == true;
  bool get hasUnitGpsFix =>
      isApiConnected &&
      lastApiSyncTime != null &&
      DateTime.now().difference(lastApiSyncTime!).inSeconds <= 25 &&
      selectedUnit?.hasFreshGps == true &&
      selectedUnit!.lastHeardSecondsAgo <= 45;
  bool get hasLiveNavigationFix =>
      hasUnitGpsFix &&
      selectedUnit?.hasNavigationHeading == true &&
      hasTarget &&
      targetGpsFresh;
  bool get hasNavigationFix => hasLiveNavigationFix || navigationIsHeld;
  bool get hasDisplayGpsFix => hasUnitGpsFix || navigationIsHeld;
  FleetUnit? get selectedUnit {
    for (final unit in allUnits) {
      if (unit.unitName.toUpperCase() == selectedUnitId.toUpperCase()) {
        return unit;
      }
    }
    return null;
  }

  Timer? _pollTimer;
  bool _starting = false;
  bool _syncing = false;
  String? _manualUnitName;
  int? _manualLocationId;
  DateTime? _lastWeatherFetch;
  int? _weatherUnitId;

  void init() {
    if (_starting || _pollTimer != null) return;
    _starting = true;
    _start();
  }

  Future<void> _start() async {
    try {
      final preferences = await SharedPreferences.getInstance();
      final savedUrl = preferences.getString('fms_backend_url');
      if (savedUrl != null && _isValidBaseUrl(Uri.tryParse(savedUrl))) {
        backendBaseUrl = savedUrl;
      }
    } catch (_) {}
    await syncFromBackend();
    _pollTimer = Timer.periodic(
      const Duration(seconds: 10),
      (_) => syncFromBackend(),
    );
  }

  void updateBackendUrl(String newUrl) {
    final uri = Uri.tryParse(newUrl.trim());
    if (!_isValidBaseUrl(uri)) {
      apiStatusMessage = 'Alamat API harus HTTPS atau HTTP jaringan lokal';
      notifyListeners();
      return;
    }
    backendBaseUrl = uri.toString().replaceFirst(RegExp(r'/$'), '');
    _resetNavigationCache();
    cabinCommsKeys.clear();
    LiveCabinCommsService().disconnect();
    SharedPreferences.getInstance().then(
      (prefs) => prefs.setString('fms_backend_url', backendBaseUrl),
    );
    _disconnect('Menghubungkan ke API...');
    syncFromBackend();
  }

  bool _isValidBaseUrl(Uri? uri) {
    if (uri == null ||
        !uri.hasAuthority ||
        uri.userInfo.isNotEmpty ||
        uri.query.isNotEmpty ||
        uri.fragment.isNotEmpty ||
        !(uri.path.isEmpty || uri.path == '/')) {
      return false;
    }
    if (uri.scheme == 'https') return true;
    if (uri.scheme != 'http') return false;
    final host = uri.host.toLowerCase();
    return host == 'localhost' ||
        host == '127.0.0.1' ||
        host.startsWith('10.') ||
        host.startsWith('192.168.') ||
        RegExp(r'^172\.(1[6-9]|2[0-9]|3[0-1])\.').hasMatch(host);
  }

  Future<Map<String, dynamic>> _get(String path) async {
    final response = await http
        .get(Uri.parse('$backendBaseUrl$path'))
        .timeout(const Duration(seconds: 12));
    if (response.statusCode == 502) {
      throw const FormatException('Tunnel/backend belum terjangkau (502)');
    }
    if (response.statusCode == 403 || response.statusCode == 401) {
      throw const FormatException('Akses API ditolak; periksa Access/VPN');
    }
    if (response.statusCode != 200) {
      throw FmsHttpException(response.statusCode);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('Respons API tidak valid');
    }
    return decoded;
  }

  List<Map<String, dynamic>> _rows(dynamic value) =>
      value is List ? value.whereType<Map<String, dynamic>>().toList() : [];

  Future<void> syncFromBackend() async {
    if (_syncing) return;
    _syncing = true;
    try {
      final fleet = await _get('/api/v1/fleet/live');
      if (fleet['status'] != 'success' || fleet['feed_stale'] == true) {
        throw const FormatException('Feed GPS tidak tersedia atau lama');
      }
      final units = _rows(fleet['data']).map(FleetUnit.fromJson).toList();
      allUnits = units;
      excavatorUnits = units.where((u) => u.category == 'Excavator').toList();
      haulerUnits = units.where((u) => u.category == 'Hauler').toList();
      isApiConnected = true;
      lastApiSyncTime = DateTime.now();
      apiStatusMessage = 'API terhubung | ${units.length} unit';
      try {
        final mtc = await _get('/api/v1/mtc/live');
        final data = mtc['data'];
        activeDispatches = data is Map<String, dynamic>
            ? _rows(data['assignments']).map(DispatchPair.fromJson).toList()
            : [];
      } catch (_) {
        activeDispatches = [];
      }
      try {
        Map<String, dynamic> locations;
        try {
          locations = await _get('/api/v1/locations/actual');
        } catch (error) {
          if (error is! FmsHttpException || error.statusCode != 404) rethrow;
          locations = await _get('/api/v1/locations/all');
        }
        final fresh = units.where((unit) => unit.hasFreshGps).toList();
        final eastings = fresh.map((unit) => unit.easting).toList()..sort();
        final northings = fresh.map((unit) => unit.northing).toList()..sort();
        final centerE = eastings.isEmpty ? 0.0 : eastings[eastings.length ~/ 2];
        final centerN = northings.isEmpty
            ? 0.0
            : northings[northings.length ~/ 2];
        miningLocations = locations['status'] == 'success' && fresh.isNotEmpty
            ? _rows(locations['data'])
                  .map(MiningLocation.fromJson)
                  .where(
                    (location) =>
                        location.easting.isFinite &&
                        location.northing.isFinite &&
                        (location.easting - centerE).abs() <= 40000 &&
                        (location.northing - centerN).abs() <= 40000,
                  )
                  .toList()
            : [];
      } catch (_) {
        miningLocations = [];
      }
      try {
        final roadsResp = await _get('/api/v1/roads/network');
        if (roadsResp['status'] == 'success') {
          roadSegments = _rows(roadsResp['data']).map(RoadSegment.fromJson).toList();
        }
      } catch (_) {}

      if (selectedUnitId.isNotEmpty) {
        try {
          final detail = await _get(
            '/api/v1/cabin/hexagon/${Uri.encodeComponent(selectedUnitId)}',
          );
          hexagonUnitData = detail['data'] is Map<String, dynamic>
              ? detail['data'] as Map<String, dynamic>
              : null;
          hexagonDataStatus = hexagonUnitData == null
              ? 'Data unit tidak tersedia'
              : 'Snapshot Hexagon tersedia';
        } on FmsHttpException catch (error) {
          hexagonUnitData = null;
          hexagonDataStatus = error.statusCode == 404
              ? 'Endpoint data kabin belum terpasang di server'
              : 'Data Hexagon gagal dimuat (HTTP ${error.statusCode})';
        } catch (_) {
          hexagonUnitData = null;
          hexagonDataStatus = 'Data Hexagon belum dapat dijangkau';
        }
      } else {
        hexagonUnitData = null;
        hexagonDataStatus = 'Pilih unit terlebih dahulu';
      }
      _updateLiveNavigationMetrics();
      _refreshNavigationDisplay();
      _updateProximityRadar();
      ensureCabinCommsPairing();
      final unit = selectedUnit;
      if (unit != null && unit.hasFreshGps) _refreshWeather(unit);
    } catch (e) {
      final detail = e is FormatException
          ? e.message
          : e is TimeoutException
          ? 'Koneksi ke server melewati batas waktu'
          : 'Periksa koneksi dan server FMS';
      _disconnect('Data FMS tidak tersedia. $detail');
    } finally {
      _syncing = false;
      notifyListeners();
    }
  }

  void _disconnect(String message) {
    isApiConnected = false;
    allUnits = [];
    excavatorUnits = [];
    haulerUnits = [];
    activeDispatches = [];
    miningLocations = [];
    hexagonUnitData = null;
    hexagonDataStatus = 'Data Hexagon tidak tersedia';
    _clearNavigation();
    _restoreHeldNavigation();
    pitWeather = 'CUACA TIDAK TERSEDIA';
    _lastWeatherFetch = null;
    _weatherUnitId = null;
    apiStatusMessage = message;
    notifyListeners();
  }

  void _clearNavigation() {
    operatorNik = '';
    operatorName = '-';
    selectedUnitModel = '';
    hdLatitude = 0;
    hdLongitude = 0;
    hdEasting = 0;
    hdNorthing = 0;
    hdElevation = 0;
    hdHeadingDeg = 0;
    hdSpeedKmh = 0;
    activeTargetName = '-';
    activeTargetType = '-';
    targetEasting = 0;
    targetNorthing = 0;
    distanceToTargetMeters = 0;
    relativeBearingDegrees = 0;
    relativeDirectionLabel = 'ARAH TIDAK TERSEDIA';
    hasTarget = false;
    targetGpsFresh = false;
    nearbyVehicles = [];
    payloadAvailable = false;
    haulDataAvailable = false;
    currentStatus = 'TIDAK TERSEDIA';
  }

  void _resetNavigationCache() {
    _lastNavigation = null;
    _usingHeldNavigation = false;
    _gpsTrack.clear();
  }

  void _refreshNavigationDisplay() {
    final truck = selectedUnit;
    if (isApiConnected &&
        truck != null &&
        truck.hasFreshGps &&
        truck.lastHeardSecondsAgo <= 45) {
      _appendGpsPoint(truck);
    }
    if (hasLiveNavigationFix && truck != null) {
      _usingHeldNavigation = false;
      _lastNavigation = _NavigationSample(
        unitName: truck.unitName,
        targetName: activeTargetName,
        targetType: activeTargetType,
        direction: relativeDirectionLabel,
        receivedAt: DateTime.now(),
        sourceAgeAtReceipt: truck.lastHeardSecondsAgo,
        latitude: hdLatitude,
        longitude: hdLongitude,
        easting: hdEasting,
        northing: hdNorthing,
        elevation: hdElevation,
        heading: hdHeadingDeg,
        speed: hdSpeedKmh,
        targetEasting: targetEasting,
        targetNorthing: targetNorthing,
        targetElevation: targetElevation,
        distance: distanceToTargetMeters,
        azimuth: absoluteTargetAzimuth,
        bearing: relativeBearingDegrees,
        eta: estimatedEtaMinutes,
      );
    } else {
      _restoreHeldNavigation();
    }
  }

  void _appendGpsPoint(FleetUnit truck) {
    final recorded = DateTime.tryParse(truck.lastHeard ?? '')?.toUtc();
    if (recorded == null ||
        recorded.isAfter(
          DateTime.now().toUtc().add(const Duration(seconds: 5)),
        )) {
      return;
    }
    if (_gpsTrack.isNotEmpty && !recorded.isAfter(_gpsTrack.last.recordedAt)) {
      return;
    }
    _gpsTrack.add(CabinGpsPoint(truck.easting, truck.northing, recorded));
    _gpsTrack.removeWhere(
      (point) =>
          recorded.difference(point.recordedAt) > const Duration(minutes: 10),
    );
    if (_gpsTrack.length > 72) _gpsTrack.removeRange(0, _gpsTrack.length - 72);
  }

  void _restoreHeldNavigation() {
    _usingHeldNavigation = false;
    final sample = _lastNavigation;
    if (sample == null ||
        sample.unitName != selectedUnitId ||
        !sample.canDisplay) {
      return;
    }
    _usingHeldNavigation = true;
    final truck = selectedUnit;
    final sourceIsLive =
        isApiConnected &&
        truck != null &&
        truck.hasNavigationHeading &&
        truck.lastHeardSecondsAgo <= 45;
    if (!sourceIsLive) {
      hdLatitude = sample.latitude;
      hdLongitude = sample.longitude;
      hdEasting = sample.easting;
      hdNorthing = sample.northing;
      hdElevation = sample.elevation;
      hdHeadingDeg = sample.heading;
      hdSpeedKmh = sample.speed;
    }
    activeTargetName = sample.targetName;
    activeTargetType = sample.targetType;
    targetEasting = sample.targetEasting;
    targetNorthing = sample.targetNorthing;
    targetElevation = sample.targetElevation;
    if (sourceIsLive) {
      final bearing = CabinBearing.fromUtm(
        unitEasting: hdEasting,
        unitNorthing: hdNorthing,
        headingDeg: hdHeadingDeg,
        targetEasting: targetEasting,
        targetNorthing: targetNorthing,
      );
      distanceToTargetMeters = bearing.distanceMeters;
      absoluteTargetAzimuth = bearing.targetAzimuthDeg;
      relativeBearingDegrees = bearing.relativeBearingDeg;
      final direction = relativeBearingDegrees.round();
      relativeDirectionLabel = direction.abs() <= 5
          ? 'LURUS'
          : direction > 0
          ? '$direction° KANAN'
          : '${direction.abs()}° KIRI';
    } else {
      distanceToTargetMeters = sample.distance;
      absoluteTargetAzimuth = sample.azimuth;
      relativeBearingDegrees = sample.bearing;
      relativeDirectionLabel = sample.direction;
    }
    estimatedEtaMinutes = 0;
    hasTarget = true;
    targetGpsFresh = false;
  }

  void _updateLiveNavigationMetrics() {
    _clearNavigation();
    final truck = selectedUnit;
    if (truck == null) {
      apiStatusMessage = 'Pilih unit dari armada API';
      return;
    }
    if (!truck.hasFreshGps) {
      apiStatusMessage =
          'GPS ${truck.unitName} lama (${truck.lastHeardSecondsAgo}s)';
      return;
    }
    hdLatitude = truck.latitude!;
    hdLongitude = truck.longitude!;
    hdEasting = truck.easting;
    hdNorthing = truck.northing;
    hdElevation = truck.elevation;
    hdHeadingDeg = truck.headingDeg;
    hdSpeedKmh = truck.speedKmh;
    currentStatus = truck.activityName.toUpperCase();
    selectedUnitModel = truck.unitType;
    operatorNik = truck.operatorId?.toString() ?? '';
    operatorName = truck.operatorId == null ? '-' : 'ID ${truck.operatorId}';
    payloadAvailable = truck.payloadAvailable;
    haulDataAvailable = truck.haulDataAvailable;
    activePayloadTons = truck.payloadTon ?? 0;
    completedRitasiCount = truck.recordedLoads ?? 0;
    if (!truck.hasNavigationHeading) {
      apiStatusMessage = 'Heading GPS ${truck.unitName} tidak tersedia';
      return;
    }

    FleetUnit? targetUnit;
    MiningLocation? targetLocation;
    bool staleAssignment = false;
    if (_manualUnitName != null) {
      for (final unit in excavatorUnits) {
        if (unit.unitName == _manualUnitName) targetUnit = unit;
      }
    } else if (_manualLocationId != null) {
      for (final loc in miningLocations) {
        if (loc.locationId == _manualLocationId) targetLocation = loc;
      }
    } else {
      DispatchPair? assignment;
      for (final item in activeDispatches) {
        if (item.truckId == truck.unitId ||
            item.truckName.toUpperCase() == truck.unitName.toUpperCase()) {
          if (assignment == null ||
              (item.updatedAt ?? '').compareTo(assignment.updatedAt ?? '') >
                  0) {
            assignment = item;
          }
        }
      }
      staleAssignment = assignment != null && !assignment.isRecent;
      if (assignment != null && assignment.isRecent) {
        final headedToDump =
            truck.hasPayload ??
            (currentStatus.contains('HAUL') || currentStatus.contains('DUMP'));
        if (headedToDump && assignment.locationId != null) {
          for (final loc in miningLocations) {
            if (loc.locationId == assignment.locationId) targetLocation = loc;
          }
        } else if (assignment.shovelId != null) {
          for (final unit in excavatorUnits) {
            if (unit.unitId == assignment.shovelId) targetUnit = unit;
          }
        }
      }
    }
    if (targetUnit != null) {
      activeTargetName = targetUnit.unitName;
      activeTargetType = 'Excavator | GPS unit';
      targetGpsFresh =
          targetUnit.hasFreshGps && targetUnit.lastHeardSecondsAgo <= 45;
      if (targetGpsFresh) {
        targetEasting = targetUnit.easting;
        targetNorthing = targetUnit.northing;
        targetElevation = targetUnit.elevation;
        hasTarget = true;
      }
    } else if (targetLocation != null &&
        targetLocation.easting > 0 &&
        targetLocation.northing > 0) {
      activeTargetName = targetLocation.name;
      activeTargetType = '${targetLocation.category} | lokasi FMS';
      targetEasting = targetLocation.easting;
      targetNorthing = targetLocation.northing;
      targetElevation = targetLocation.elevation;
      targetGpsFresh = true;
      hasTarget = true;
    }
    if (!hasTarget) {
      relativeDirectionLabel = activeTargetName == '-'
          ? staleAssignment
                ? 'ASSIGNMENT LAMA'
                : 'TARGET BELUM DITETAPKAN'
          : 'GPS TARGET TIDAK AKTIF';
      return;
    }
    final bearing = CabinBearing.fromUtm(
      unitEasting: hdEasting,
      unitNorthing: hdNorthing,
      headingDeg: hdHeadingDeg,
      targetEasting: targetEasting,
      targetNorthing: targetNorthing,
    );
    distanceToTargetMeters = bearing.distanceMeters;
    absoluteTargetAzimuth = bearing.targetAzimuthDeg;
    relativeBearingDegrees = bearing.relativeBearingDeg;
    final direction = relativeBearingDegrees.round();
    relativeDirectionLabel = direction.abs() <= 5
        ? 'LURUS'
        : direction > 0
        ? '$direction° KANAN'
        : '${direction.abs()}° KIRI';
    estimatedEtaMinutes = hdSpeedKmh > 5
        ? distanceToTargetMeters / (hdSpeedKmh * 1000 / 3600) / 60
        : 0;
  }

  void _updateProximityRadar() {
    if (!hasLiveNavigationFix) {
      nearbyVehicles = [];
      return;
    }
    final seen = <int>{selectedUnit!.unitId};
    final vehicles = <NearbyVehicle>[];
    for (final unit in allUnits) {
      if (!unit.hasFreshGps ||
          unit.lastHeardSecondsAgo > 45 ||
          (activeTargetType.startsWith('Excavator') &&
              unit.unitName == activeTargetName) ||
          !seen.add(unit.unitId)) {
        continue;
      }
      final dE = unit.easting - hdEasting, dN = unit.northing - hdNorthing;
      final distance = math.sqrt(dE * dE + dN * dN);
      if (distance > 450) continue;
      final offset = CabinBearing.headingUpOffset(
        eastMeters: dE,
        northMeters: dN,
        headingDeg: hdHeadingDeg,
      );
      vehicles.add(
        NearbyVehicle(
          unitName: unit.unitName,
          unitType: unit.category == 'Excavator'
              ? 'EX'
              : unit.category == 'Hauler'
              ? 'HD'
              : 'UNIT',
          lateralOffsetMeters: offset.$1,
          forwardOffsetMeters: offset.$2,
          distanceMeters: distance,
        ),
      );
    }
    vehicles.sort((a, b) => a.distanceMeters.compareTo(b.distanceMeters));
    nearbyVehicles = vehicles.take(6).toList();
  }

  Future<void> _refreshWeather(FleetUnit unit) async {
    if (_weatherUnitId == unit.unitId &&
        _lastWeatherFetch != null &&
        DateTime.now().difference(_lastWeatherFetch!).inMinutes < 5) {
      return;
    }
    _weatherUnitId = unit.unitId;
    pitWeather = 'CUACA DIPERBARUI...';
    _lastWeatherFetch = DateTime.now();
    try {
      final weather = await _get(
        '/api/v1/weather/current?latitude=${unit.latitude}&longitude=${unit.longitude}',
      );
      final data = weather['data'];
      if (weather['status'] == 'success' && data is Map<String, dynamic>) {
        final rain = data['isRaining'] == true || data['is_raining'] == true;
        final temp = data['temperatureC'] ?? data['temperature_c'];
        pitWeather =
            '${rain ? 'HUJAN' : 'TIDAK HUJAN'} | ${temp is num ? '${temp.toStringAsFixed(0)} C' : 'SUHU -'}';
        notifyListeners();
      }
    } catch (_) {
      pitWeather = 'CUACA TIDAK TERSEDIA';
      notifyListeners();
    }
  }

  bool login(String nik, String name, String unitId) {
    if (!isApiConnected ||
        !haulerUnits.any((u) => u.unitName == unitId && u.hasFreshGps)) {
      return false;
    }
    ensureCabinCommsPairing(unitId);
    if (selectedUnitId != unitId) _resetNavigationCache();
    selectedUnitId = unitId;
    hexagonUnitData = null;
    hexagonDataStatus = 'Memuat data Hexagon...';
    _lastWeatherFetch = null;
    pitWeather = 'CUACA TIDAK TERSEDIA';
    isLoggedIn = true;
    _manualUnitName = null;
    _manualLocationId = null;
    isManualTargetOverride = false;
    _updateLiveNavigationMetrics();
    _refreshNavigationDisplay();
    _updateProximityRadar();
    notifyListeners();
    syncFromBackend();
    return true;
  }

  Future<String?> ensureCabinCommsPairing([String? targetUnit]) async {
    final unitId = (targetUnit ?? selectedUnitId).trim();
    if (unitId.isEmpty) return null;
    final existing = cabinCommsKeys[unitId];
    if (existing != null && existing.isNotEmpty) {
      await LiveCabinCommsService().connect(backendBaseUrl, unitId, existing);
      return existing;
    }
    try {
      final response = await http
          .post(
            Uri.parse('$backendBaseUrl/api/v1/comms/pair'),
            headers: {
              'X-FMS-Dispatcher-Key': _localDispatcherKey,
              'Content-Type': 'application/json',
            },
            body: jsonEncode({'unit_name': unitId}),
          )
          .timeout(const Duration(seconds: 8));
      if (response.statusCode != 200) {
        LiveCabinCommsService().disconnect();
        return null;
      }
      final decoded = jsonDecode(response.body);
      if (decoded is! Map<String, dynamic>) return null;
      final token = decoded['token']?.toString();
      if (token == null || token.isEmpty) return null;
      cabinCommsKeys[unitId] = token;
      await LiveCabinCommsService().connect(backendBaseUrl, unitId, token);
      return token;
    } catch (_) {
      LiveCabinCommsService().disconnect();
      return null;
    }
  }

  void logout() {
    isLoggedIn = false;
    cabinCommsKeys.clear();
    LiveCabinCommsService().disconnect();
    selectedUnitId = '';
    _resetNavigationCache();
    hexagonUnitData = null;
    hexagonDataStatus = 'Pilih unit terlebih dahulu';
    _clearNavigation();
    notifyListeners();
  }

  void setManualTarget(
    String targetName,
    String targetType,
    double easting,
    double northing,
  ) {
    FleetUnit? unit;
    for (final item in excavatorUnits) {
      if (item.unitName == targetName && item.hasFreshGps) unit = item;
    }
    MiningLocation? loc;
    for (final item in miningLocations) {
      if (item.name == targetName && item.easting > 0 && item.northing > 0) {
        loc = item;
      }
    }
    if (unit == null && loc == null) return;
    _manualUnitName = unit?.unitName;
    _manualLocationId = loc?.locationId;
    isManualTargetOverride = true;
    _lastNavigation = null;
    _updateLiveNavigationMetrics();
    _refreshNavigationDisplay();
    _updateProximityRadar();
    notifyListeners();
  }

  void clearManualTarget() {
    _manualUnitName = null;
    _manualLocationId = null;
    isManualTargetOverride = false;
    _lastNavigation = null;
    _updateLiveNavigationMetrics();
    _refreshNavigationDisplay();
    notifyListeners();
  }

  @override
  void dispose() {
    _pollTimer?.cancel();
    super.dispose();
  }
}

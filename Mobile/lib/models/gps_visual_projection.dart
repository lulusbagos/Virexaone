import 'dart:math' as math;

class GpsVisualPosition {
  final double easting;
  final double northing;
  const GpsVisualPosition(this.easting, this.northing);
}

class GpsVisualProjection {
  static const int horizonSeconds = 8;
  GpsVisualPosition? _fix;
  GpsVisualPosition? _display;
  DateTime? _recordedAt;
  DateTime? _receivedAt;
  DateTime? _lastRenderedAt;
  int _sourceAgeSeconds = 0;
  double _speedKmh = 0;
  double _headingDeg = 0;
  bool _headingAvailable = false;

  GpsVisualPosition? get position => _display;
  bool isPredicting(DateTime now) {
    final fix = _fix;
    final display = _display;
    final received = _receivedAt;
    return fix != null &&
        display != null &&
        received != null &&
        _sourceAgeSeconds + now.difference(received).inSeconds <= 25 &&
        math.sqrt(
              math.pow(display.easting - fix.easting, 2) +
                  math.pow(display.northing - fix.northing, 2),
            ) >
            1;
  }

  void clear() {
    _fix = null;
    _display = null;
    _recordedAt = null;
    _receivedAt = null;
    _lastRenderedAt = null;
  }

  bool accept({
    required double easting,
    required double northing,
    required DateTime recordedAt,
    required DateTime receivedAt,
    required int sourceAgeSeconds,
    required double speedKmh,
    required double headingDeg,
    required bool headingAvailable,
  }) {
    if (!easting.isFinite ||
        !northing.isFinite ||
        easting <= 0 ||
        northing <= 0 ||
        sourceAgeSeconds < 0 ||
        recordedAt.isAfter(receivedAt.add(const Duration(seconds: 5)))) {
      return false;
    }
    final previousAt = _recordedAt;
    if (previousAt != null) {
      final gap = recordedAt.difference(previousAt).inMilliseconds / 1000;
      if (gap <= 0) return false;
      if (gap > 120) clear();
      final previous = _fix;
      if (previous != null && gap <= 120) {
        final distance = math.sqrt(
          math.pow(easting - previous.easting, 2) +
              math.pow(northing - previous.northing, 2),
        );
        if (distance > 25 * gap + 30) return false;
      }
    }
    _fix = GpsVisualPosition(easting, northing);
    _display ??= _fix;
    _recordedAt = recordedAt;
    _receivedAt = receivedAt;
    _sourceAgeSeconds = sourceAgeSeconds;
    _speedKmh = speedKmh;
    _headingDeg = headingDeg;
    _headingAvailable = headingAvailable;
    return true;
  }

  GpsVisualPosition? advance(DateTime now) {
    final fix = _fix;
    final received = _receivedAt;
    if (fix == null || received == null) return null;
    final elapsed = math.max(
      0.0,
      now.difference(received).inMilliseconds / 1000,
    );
    double distance = 0;
    if (_sourceAgeSeconds <= 20 &&
        _headingAvailable &&
        _headingDeg.isFinite &&
        _headingDeg >= 0 &&
        _headingDeg <= 360 &&
        _speedKmh.isFinite &&
        _speedKmh > 0.5 &&
        _speedKmh <= 90) {
      final predictionTime = math.min(
        elapsed,
        math.min(
          horizonSeconds.toDouble(),
          (20 - _sourceAgeSeconds).toDouble(),
        ),
      );
      distance = math.min(20.0, _speedKmh / 3.6 * predictionTime);
    }
    final radians = _headingDeg * math.pi / 180;
    final target = GpsVisualPosition(
      fix.easting + math.sin(radians) * distance,
      fix.northing + math.cos(radians) * distance,
    );
    final previous = _display ?? fix;
    final dt = _lastRenderedAt == null
        ? 0.0
        : (now.difference(_lastRenderedAt!).inMilliseconds / 1000).clamp(
            0.0,
            1.0,
          );
    final blend = 1 - math.exp(-dt / 0.6);
    _display = GpsVisualPosition(
      previous.easting + (target.easting - previous.easting) * blend,
      previous.northing + (target.northing - previous.northing) * blend,
    );
    _lastRenderedAt = now;
    return _display;
  }
}

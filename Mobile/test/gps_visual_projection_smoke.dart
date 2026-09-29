import 'package:virexa_mobile/models/gps_visual_projection.dart';

void main() {
  final now = DateTime.utc(2026, 9, 28, 10);
  final projection = GpsVisualProjection();
  assert(projection.accept(
    easting: 500000,
    northing: 9800000,
    recordedAt: now,
    receivedAt: now,
    sourceAgeSeconds: 0,
    speedKmh: 36,
    headingDeg: 90,
    headingAvailable: true,
  ));
  projection.advance(now);
  final projected = projection.advance(now.add(const Duration(seconds: 4)))!;
  assert(projected.easting > 500010 && projected.easting <= 500020);
  assert(projected.northing == 9800000);
  final held = projection.advance(now.add(const Duration(seconds: 30)))!;
  assert(held.easting <= 500020);
  assert(!projection.accept(
    easting: 501000,
    northing: 9800000,
    recordedAt: now.add(const Duration(seconds: 31)),
    receivedAt: now.add(const Duration(seconds: 31)),
    sourceAgeSeconds: 0,
    speedKmh: 0,
    headingDeg: 0,
    headingAvailable: false,
  ));
}

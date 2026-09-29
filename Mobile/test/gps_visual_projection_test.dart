import 'package:flutter_test/flutter_test.dart';
import 'package:virexa_mobile/models/gps_visual_projection.dart';

void main() {
  final now = DateTime.utc(2026, 9, 28, 10);

  test('projects briefly from actual GPS and stops at the horizon', () {
    final projection = GpsVisualProjection();
    expect(
      projection.accept(
        easting: 500000,
        northing: 9800000,
        recordedAt: now,
        receivedAt: now,
        sourceAgeSeconds: 0,
        speedKmh: 36,
        headingDeg: 90,
        headingAvailable: true,
      ),
      isTrue,
    );
    projection.advance(now);
    final moving = projection.advance(now.add(const Duration(seconds: 4)))!;
    expect(moving.easting, greaterThan(500010));
    expect(moving.easting, lessThanOrEqualTo(500020));
    expect(moving.northing, closeTo(9800000, 0.01));
    final stopped = projection.advance(now.add(const Duration(seconds: 30)))!;
    expect(stopped.easting, lessThanOrEqualTo(500020));
    expect(
      projection.isPredicting(now.add(const Duration(seconds: 30))),
      isFalse,
    );
  });

  test('missing heading prevents prediction; new fix corrects smoothly', () {
    final projection = GpsVisualProjection();
    projection.accept(
      easting: 500000,
      northing: 9800000,
      recordedAt: now,
      receivedAt: now,
      sourceAgeSeconds: 0,
      speedKmh: 36,
      headingDeg: 0,
      headingAvailable: false,
    );
    projection.advance(now);
    expect(
      projection.advance(now.add(const Duration(seconds: 3)))!.northing,
      9800000,
    );
    expect(
      projection.accept(
        easting: 500012,
        northing: 9800000,
        recordedAt: now.add(const Duration(seconds: 10)),
        receivedAt: now.add(const Duration(seconds: 10)),
        sourceAgeSeconds: 0,
        speedKmh: 20,
        headingDeg: 90,
        headingAvailable: true,
      ),
      isTrue,
    );
    final corrected = projection.advance(now.add(const Duration(seconds: 10)))!;
    expect(corrected.easting, greaterThan(500000));
    expect(corrected.easting, lessThan(500012));
  });

  test('rejects implausible jump and duplicate fix', () {
    final projection = GpsVisualProjection();
    projection.accept(
      easting: 500000,
      northing: 9800000,
      recordedAt: now,
      receivedAt: now,
      sourceAgeSeconds: 0,
      speedKmh: 0,
      headingDeg: 0,
      headingAvailable: true,
    );
    expect(
      projection.accept(
        easting: 501000,
        northing: 9800000,
        recordedAt: now.add(const Duration(seconds: 10)),
        receivedAt: now.add(const Duration(seconds: 10)),
        sourceAgeSeconds: 0,
        speedKmh: 0,
        headingDeg: 0,
        headingAvailable: true,
      ),
      isFalse,
    );
    expect(
      projection.accept(
        easting: 500000,
        northing: 9800000,
        recordedAt: now,
        receivedAt: now,
        sourceAgeSeconds: 0,
        speedKmh: 0,
        headingDeg: 0,
        headingAvailable: true,
      ),
      isFalse,
    );
  });
}

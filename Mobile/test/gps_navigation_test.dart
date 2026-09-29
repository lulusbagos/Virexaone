import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:virexa_mobile/services/fms_api_service.dart';
import 'package:virexa_mobile/models/gps_hold_policy.dart';
import 'package:virexa_mobile/widgets/gps_track_painter.dart';

void main() {
  test('held GPS expires after a short gap or an old source sample', () {
    final now = DateTime.utc(2026, 9, 27, 10);
    expect(
      canDisplayHeldGps(now.subtract(const Duration(seconds: 24)), 20, now),
      isTrue,
    );
    expect(
      canDisplayHeldGps(now.subtract(const Duration(seconds: 26)), 20, now),
      isFalse,
    );
    expect(
      canDisplayHeldGps(now.subtract(const Duration(seconds: 20)), 55, now),
      isFalse,
    );
    expect(
      canDisplayHeldGps(now.add(const Duration(seconds: 1)), 10, now),
      isFalse,
    );
  });

  testWidgets('GPS track renders at cockpit and compact sizes', (tester) async {
    final now = DateTime.utc(2026, 9, 27, 10);
    final points = [
      CabinGpsPoint(510000, 9840000, now.subtract(const Duration(seconds: 20))),
      CabinGpsPoint(510025, 9840030, now.subtract(const Duration(seconds: 10))),
      CabinGpsPoint(510050, 9840060, now),
    ];
    for (final size in [const Size(800, 320), const Size(320, 180)]) {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Center(
              child: SizedBox(
                width: size.width,
                height: size.height,
                child: CustomPaint(
                  painter: GpsTrackPainter(
                    track: points,
                    nearbyVehicles: [
                      NearbyVehicle(
                        unitName: 'RD4015',
                        unitType: 'HD',
                        lateralOffsetMeters: 35,
                        forwardOffsetMeters: 45,
                        distanceMeters: 57,
                      ),
                      NearbyVehicle(
                        unitName: 'EX6002',
                        unitType: 'EX',
                        lateralOffsetMeters: -80,
                        forwardOffsetMeters: 20,
                        distanceMeters: 82,
                      ),
                    ],
                    easting: 510050,
                    northing: 9840060,
                    heading: 40,
                    targetEasting: 512000,
                    targetNorthing: 9841800,
                    targetName: 'Disposal Utara 04',
                    held: size.width < 400,
                  ),
                ),
              ),
            ),
          ),
        ),
      );
      expect(tester.takeException(), isNull);
    }
  });
}

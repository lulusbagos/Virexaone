import 'dart:math' as math;

import 'package:flutter_test/flutter_test.dart';
import 'package:virexa_mobile/models/cabin_bearing.dart';
import 'package:virexa_mobile/models/fleet_models.dart';

void main() {
  test('UTM north/east and relative heading are compass-aligned', () {
    final east = CabinBearing.fromUtm(
      unitEasting: 100,
      unitNorthing: 100,
      headingDeg: 0,
      targetEasting: 200,
      targetNorthing: 100,
    );
    expect(east.distanceMeters, 100);
    expect(east.targetAzimuthDeg, 90);
    expect(east.relativeBearingDeg, 90);

    final ahead = CabinBearing.fromUtm(
      unitEasting: 100,
      unitNorthing: 100,
      headingDeg: 90,
      targetEasting: 200,
      targetNorthing: 100,
    );
    expect(ahead.relativeBearingDeg, 0);

    final left = CabinBearing.fromUtm(
      unitEasting: 100,
      unitNorthing: 100,
      headingDeg: 0,
      targetEasting: 0,
      targetNorthing: 100,
    );
    expect(left.relativeBearingDeg, -90);
  });

  test('missing heading is different from a valid zero-degree heading', () {
    final data = <String, dynamic>{
      'unit_id': 1,
      'unit_name': 'RD1',
      'latitude': -1.1,
      'longitude': 116.8,
      'easting': 500000,
      'northing': 100000,
      'heading_deg': 0,
      'last_heard_seconds_ago': 10,
    };
    expect(FleetUnit.fromJson(data).hasNavigationHeading, isFalse);
    data['heading_available'] = true;
    expect(FleetUnit.fromJson(data).hasNavigationHeading, isTrue);
    data['last_heard_seconds_ago'] = 121;
    expect(FleetUnit.fromJson(data).hasNavigationHeading, isFalse);
  });

  test('track offsets and arrow bearing share one heading-up direction', () {
    for (final heading in [0.0, 30.0, 90.0, 180.0, 270.0]) {
      for (final delta in [
        (100.0, 0.0),
        (50.0, 90.0),
        (-80.0, 40.0),
        (-25.0, -70.0),
      ]) {
        final offset = CabinBearing.headingUpOffset(
          eastMeters: delta.$1,
          northMeters: delta.$2,
          headingDeg: heading,
        );
        final bearing = CabinBearing.fromUtm(
          unitEasting: 500000,
          unitNorthing: 100000,
          headingDeg: heading,
          targetEasting: 500000 + delta.$1,
          targetNorthing: 100000 + delta.$2,
        );
        final screenAngle =
            (math.atan2(offset.$1, offset.$2) * 180 / math.pi + 360) % 360;
        final arrowAngle = (bearing.relativeBearingDeg + 360) % 360;
        expect((screenAngle - arrowAngle).abs(), lessThan(0.0001));
      }
    }
  });
}

import 'dart:io';
import 'dart:math' as math;
import 'package:virexa_mobile/models/cabin_bearing.dart';
import 'package:virexa_mobile/models/fleet_models.dart';

void main() {
  final east = CabinBearing.fromUtm(
    unitEasting: 100,
    unitNorthing: 100,
    headingDeg: 0,
    targetEasting: 200,
    targetNorthing: 100,
  );
  assert(east.distanceMeters == 100);
  assert(east.targetAzimuthDeg == 90);
  assert(east.relativeBearingDeg == 90);

  final left = CabinBearing.fromUtm(
    unitEasting: 100,
    unitNorthing: 100,
    headingDeg: 0,
    targetEasting: 0,
    targetNorthing: 100,
  );
  assert(left.relativeBearingDeg == -90);

  for (final heading in [0.0, 30.0, 90.0, 187.0, 270.0]) {
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
      final trackAngle = math.atan2(offset.$1, offset.$2);
      final arrowAngle = bearing.relativeBearingDeg * math.pi / 180;
      assert((math.sin(trackAngle - arrowAngle)).abs() < 0.000001);
      assert((math.cos(trackAngle - arrowAngle) - 1).abs() < 0.000001);
    }
  }

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
  assert(!FleetUnit.fromJson(data).hasNavigationHeading);
  data['heading_available'] = true;
  assert(FleetUnit.fromJson(data).hasNavigationHeading);
  data['last_heard_seconds_ago'] = 121;
  assert(!FleetUnit.fromJson(data).hasNavigationHeading);
  stdout.writeln('Navigation geometry and GPS freshness checks passed.');
}

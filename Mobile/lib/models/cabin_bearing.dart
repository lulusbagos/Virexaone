import 'dart:math' as math;

class CabinBearing {
  final double distanceMeters;
  final double targetAzimuthDeg;
  final double relativeBearingDeg;

  const CabinBearing(
    this.distanceMeters,
    this.targetAzimuthDeg,
    this.relativeBearingDeg,
  );

  static (double lateralMeters, double forwardMeters) headingUpOffset({
    required double eastMeters,
    required double northMeters,
    required double headingDeg,
  }) {
    final heading = headingDeg * math.pi / 180;
    return (
      eastMeters * math.cos(heading) - northMeters * math.sin(heading),
      eastMeters * math.sin(heading) + northMeters * math.cos(heading),
    );
  }

  static CabinBearing fromUtm({
    required double unitEasting,
    required double unitNorthing,
    required double headingDeg,
    required double targetEasting,
    required double targetNorthing,
  }) {
    final east = targetEasting - unitEasting;
    final north = targetNorthing - unitNorthing;
    final distance = math.sqrt(east * east + north * north);
    final azimuth = (math.atan2(east, north) * 180 / math.pi + 360) % 360;
    final offset = headingUpOffset(
      eastMeters: east,
      northMeters: north,
      headingDeg: headingDeg,
    );
    final rawRelative = math.atan2(offset.$1, offset.$2) * 180 / math.pi;
    final relative = rawRelative >= 180 ? rawRelative - 360 : rawRelative;
    return CabinBearing(distance, azimuth, relative);
  }
}

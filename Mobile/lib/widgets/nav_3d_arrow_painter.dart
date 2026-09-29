import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../services/fms_api_service.dart';
import '../theme/fms_theme.dart';

/// 3D Vector for Navigational Geometry
class Vec3 {
  final double x; // Lateral (Left = -X, Right = +X)
  final double y; // Vertical Height (Ground = 0, Sky = +Y)
  final double z; // Forward Road Distance (Behind = -Z, Forward/Horizon = +Z)

  const Vec3(this.x, this.y, this.z);

  /// Rotate around vertical Y-axis by angle in radians (Clockwise looking from above)
  Vec3 rotateY(double rad) {
    final cosA = math.cos(rad);
    final sinA = math.sin(rad);
    return Vec3(x * cosA + z * sinA, y, -x * sinA + z * cosA);
  }

  /// Rotate around forward Z-axis by angle in radians (Roll banking)
  Vec3 rotateZ(double rad) {
    final cosA = math.cos(rad);
    final sinA = math.sin(rad);
    return Vec3(x * cosA - y * sinA, x * sinA + y * cosA, z);
  }
}

class Nav3dArrowPainter extends CustomPainter {
  final double relativeBearing; // Bearing to target in degrees (-180° to +180°)
  final double absoluteHeading; // Truck compass heading (0° to 360°)
  final double targetAzimuth; // Target absolute azimuth (0° to 360°)
  final double animPhase; // Pulse animation phase (0.0 to 1.0)
  final List<NearbyVehicle>? nearbyVehicles;

  Nav3dArrowPainter({
    required this.relativeBearing,
    required this.absoluteHeading,
    this.targetAzimuth = 0.0,
    required this.animPhase,
    this.nearbyVehicles,
  });

  // Elevated chase view: the near edge of the ground plane is visibly larger.
  static const double camPitchDeg = 52.0;
  static const double camDistOffset = 2.65;
  static const double camFocalLen = 2.25;

  /// Project 3D coordinate (X=lateral, Y=up, Z=forward) to 2D Screen Space
  Offset _project(Vec3 pt, Offset center, double scale) {
    final pitchRad = camPitchDeg * math.pi / 180.0;
    final sinP = math.sin(pitchRad);
    final cosP = math.cos(pitchRad);

    // Forward road depth and vertical perspective
    final vertCam = pt.z * sinP + pt.y * cosP;
    final depthCam = pt.z * cosP - pt.y * sinP + camDistOffset;

    final depth = depthCam > 0.3 ? depthCam : 0.3;
    final factor = (camFocalLen / depth) * scale;

    // Match the heading-up track's screen bearing despite the camera pitch.
    final screenX = center.dx + pt.x * sinP * factor;
    final screenY = center.dy - vertCam * factor;

    return Offset(screenX, screenY);
  }

  @override
  void paint(Canvas canvas, Size size) {
    canvas.save();
    canvas.clipRect(Offset.zero & size);
    final center = Offset(size.width / 2, size.height * 0.60);
    final viewScale = math.min(size.width, size.height) * 0.44;

    // 1. 3D Perspective Ground Plane Radar & Range Rings
    _draw3DGroundRadar(canvas, center, viewScale);

    // 2. 3D Waypoint Beacon & Landing Pad at Target Azimuth
    _draw3DTargetBeacon(canvas, center, viewScale);

    // 3. Precise relative bearing from the selected unit's FMS GPS heading.
    _drawVolumetric3DArrow(canvas, center, viewScale);

    // 4. Nearby unit markers stay legible above the guidance arrow.
    _drawNearbyVehicles(canvas, center, viewScale, size);

    // 5. Tactical Cockpit Reticle Overlay
    _drawCockpitReticle(canvas, size);
    canvas.restore();
  }

  // =========================================================================
  // 1. 3D PERSPECTIVE GROUND RADAR GRID
  // =========================================================================
  void _draw3DGroundRadar(Canvas canvas, Offset center, double scale) {
    // Ambient Ground Glow
    final glowPaint = Paint()
      ..shader =
          RadialGradient(
            colors: [
              const Color(0x2200FFA3),
              const Color(0x0C00E5FF),
              Colors.transparent,
            ],
            stops: const [0.0, 0.55, 1.0],
          ).createShader(
            Rect.fromCenter(
              center: center,
              width: scale * 3.0,
              height: scale * 2.0,
            ),
          );
    canvas.drawOval(
      Rect.fromCenter(center: center, width: scale * 3.0, height: scale * 2.0),
      glowPaint,
    );

    // Schematic guide rings; only unit tags show measured distances.
    final radii = [0.60, 1.15, 1.70];

    for (int i = 0; i < radii.length; i++) {
      final r = radii[i];
      final ringPath = Path();
      const segments = 48;

      for (int s = 0; s <= segments; s++) {
        final a = (s / segments) * 2 * math.pi;
        final pt = _project(
          Vec3(math.sin(a) * r, 0.0, math.cos(a) * r),
          center,
          scale,
        );
        if (s == 0) {
          ringPath.moveTo(pt.dx, pt.dy);
        } else {
          ringPath.lineTo(pt.dx, pt.dy);
        }
      }

      final isOuter = i == radii.length - 1;
      final ringPaint = Paint()
        ..color = isOuter
            ? FmsTheme.cyanAccent.withValues(alpha: 0.45)
            : const Color(0x2E00E5FF)
        ..style = PaintingStyle.stroke
        ..strokeWidth = isOuter ? 1.2 : 0.8;
      canvas.drawPath(ringPath, ringPaint);
    }

    // Dynamic Rotating 12-Radial Compass Grid Lines (Rotates with truck heading)
    final truckRad = absoluteHeading * math.pi / 180.0;
    final gridLinePaint = Paint()
      ..color = const Color(0x1F00FFA3)
      ..strokeWidth = 0.8;

    for (int i = 0; i < 12; i++) {
      final lineAngle = (i * 30.0 * math.pi / 180.0) - truckRad;
      final p1 = _project(
        Vec3(math.sin(lineAngle) * 0.35, 0.0, math.cos(lineAngle) * 0.35),
        center,
        scale,
      );
      final p2 = _project(
        Vec3(math.sin(lineAngle) * 1.70, 0.0, math.cos(lineAngle) * 1.70),
        center,
        scale,
      );
      canvas.drawLine(p1, p2, gridLinePaint);

      // Cardinal Labels (N, E, S, W) on Ground Plane
      if (i % 3 == 0) {
        final cardAngle = lineAngle;
        final cardPos = _project(
          Vec3(math.sin(cardAngle) * 1.88, 0.0, math.cos(cardAngle) * 1.88),
          center,
          scale,
        );
        final cardLabels = ["N", "E", "S", "W"];
        final label = cardLabels[i ~/ 3];

        final cTp = TextPainter(
          text: TextSpan(
            text: label,
            style: TextStyle(
              color: label == "N" ? FmsTheme.amberWarning : FmsTheme.cyanAccent,
              fontSize: 10,
              fontWeight: FontWeight.w900,
            ),
          ),
          textDirection: TextDirection.ltr,
        )..layout();
        cTp.paint(canvas, Offset(cardPos.dx - (cTp.width / 2), cardPos.dy - 6));
      }
    }

    // Perspective Haul Road Lanes (Forward Driving Axis)
    final lanePaint = Paint()
      ..shader =
          LinearGradient(
            begin: Alignment.bottomCenter,
            end: Alignment.topCenter,
            colors: [
              FmsTheme.cyanAccent.withValues(alpha: 0.50),
              FmsTheme.cyanAccent.withValues(alpha: 0.05),
            ],
          ).createShader(
            Rect.fromLTRB(
              center.dx - 80,
              center.dy - 100,
              center.dx + 80,
              center.dy + 70,
            ),
          )
      ..strokeWidth = 1.1;

    final leftLane1 = _project(const Vec3(-0.45, 0.0, -0.4), center, scale);
    final leftLane2 = _project(const Vec3(-0.85, 0.0, 2.0), center, scale);
    final rightLane1 = _project(const Vec3(0.45, 0.0, -0.4), center, scale);
    final rightLane2 = _project(const Vec3(0.85, 0.0, 2.0), center, scale);

    canvas.drawLine(leftLane1, leftLane2, lanePaint);
    canvas.drawLine(rightLane1, rightLane2, lanePaint);
  }

  // =========================================================================
  // 2. 3D TARGET WAYPOINT BEACON & FLOATING CRYSTAL
  // =========================================================================
  void _draw3DTargetBeacon(Canvas canvas, Offset center, double scale) {
    // The primary arrow handles rear targets; no displaced beacon is drawn.
    if (relativeBearing.abs() > 88.0) return;
    final rad = relativeBearing * math.pi / 180.0;
    final tgtX = math.sin(rad) * 1.65;
    final tgtZ = math.cos(rad) * 1.65;

    final groundPt = _project(Vec3(tgtX, 0.0, tgtZ), center, scale);
    final hoverPt = _project(Vec3(tgtX, 0.48, tgtZ), center, scale);

    // Ground Landing Ring
    final ringPaint = Paint()
      ..color = FmsTheme.emeraldGreen.withValues(alpha: 0.75)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.5;

    final rPath = Path();
    for (int s = 0; s <= 24; s++) {
      final a = (s / 24) * 2 * math.pi;
      final rx = tgtX + math.sin(a) * 0.16;
      final rz = tgtZ + math.cos(a) * 0.16;
      final pt = _project(Vec3(rx, 0.0, rz), center, scale);
      if (s == 0) {
        rPath.moveTo(pt.dx, pt.dy);
      } else {
        rPath.lineTo(pt.dx, pt.dy);
      }
    }
    canvas.drawPath(rPath, ringPaint);

    // Holographic Vertical Pillar
    final laserPaint = Paint()
      ..shader =
          LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [
              Colors.white,
              FmsTheme.cyanAccent,
              FmsTheme.emeraldGreen.withValues(alpha: 0.1),
            ],
          ).createShader(
            Rect.fromLTRB(
              hoverPt.dx - 2,
              hoverPt.dy,
              hoverPt.dx + 2,
              groundPt.dy,
            ),
          )
      ..strokeWidth = 2.0;
    canvas.drawLine(hoverPt, groundPt, laserPaint);

    // 3D Floating Diamond Crystal
    final topApex = _project(Vec3(tgtX, 0.60, tgtZ), center, scale);
    final btmApex = _project(Vec3(tgtX, 0.36, tgtZ), center, scale);
    final leftCorner = _project(Vec3(tgtX - 0.10, 0.48, tgtZ), center, scale);
    final rightCorner = _project(Vec3(tgtX + 0.10, 0.48, tgtZ), center, scale);
    final frontCorner = _project(Vec3(tgtX, 0.48, tgtZ - 0.08), center, scale);

    // Left Facet
    final leftFacet = Path()
      ..moveTo(topApex.dx, topApex.dy)
      ..lineTo(leftCorner.dx, leftCorner.dy)
      ..lineTo(btmApex.dx, btmApex.dy)
      ..lineTo(frontCorner.dx, frontCorner.dy)
      ..close();
    canvas.drawPath(leftFacet, Paint()..color = const Color(0xFF00FFA3));

    // Right Facet
    final rightFacet = Path()
      ..moveTo(topApex.dx, topApex.dy)
      ..lineTo(frontCorner.dx, frontCorner.dy)
      ..lineTo(btmApex.dx, btmApex.dy)
      ..lineTo(rightCorner.dx, rightCorner.dy)
      ..close();
    canvas.drawPath(rightFacet, Paint()..color = const Color(0xFF00B4D8));

    // Facet Outline
    final wirePaint = Paint()
      ..color = Colors.white
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.1;
    canvas.drawPath(leftFacet, wirePaint);
    canvas.drawPath(rightFacet, wirePaint);
  }

  // =========================================================================
  // 3. PROXIMITY RADAR BLIPS FOR NEARBY MINING FLEET (CAS)
  // =========================================================================
  void _drawNearbyVehicles(
    Canvas canvas,
    Offset center,
    double scale,
    Size size,
  ) {
    if (nearbyVehicles == null || nearbyVehicles!.isEmpty) return;
    final railWidth = math.min(150.0, size.width * 0.32);
    final railX = relativeBearing >= 0 ? 16.0 : size.width - railWidth - 16.0;
    final railY = math.max(64.0, size.height * 0.18);
    final visibleCount = math.min(
      nearbyVehicles!.length,
      math.max(1, ((size.height - railY - 145) / 24).floor()),
    );

    for (int i = 0; i < nearbyVehicles!.length; i++) {
      final v = nearbyVehicles![i];
      final vx = v.lateralOffsetMeters / 300.0;
      final vz = v.forwardOffsetMeters / 300.0;
      final pt = _project(Vec3(vx, 0.0, vz), center, scale);
      final Color unitColor;
      if (v.unitType == "LV") {
        unitColor = FmsTheme.amberWarning;
      } else if (v.unitType == "EX") {
        unitColor = const Color(0xFFFF007F);
      } else {
        unitColor = FmsTheme.cyanAccent;
      }
      final pulseRadius = 11.0 + (math.sin(animPhase * 3 * math.pi) * 2.0);
      canvas.drawCircle(
        pt,
        pulseRadius,
        Paint()
          ..color = unitColor.withValues(alpha: 0.75)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.3,
      );
      canvas.drawCircle(pt, 7, Paint()..color = const Color(0xFF06131F));
      canvas.drawCircle(
        pt,
        7,
        Paint()
          ..color = unitColor
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.5,
      );
      if (i >= visibleCount) continue;
      final indexLabel = TextPainter(
        text: TextSpan(
          text: '${i + 1}',
          style: const TextStyle(
            color: Colors.white,
            fontSize: 9,
            fontWeight: FontWeight.bold,
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      indexLabel.paint(
        canvas,
        pt - Offset(indexLabel.width / 2, indexLabel.height / 2),
      );

      final row = Rect.fromLTWH(railX, railY + i * 24.0, railWidth, 21);
      canvas.drawRRect(
        RRect.fromRectAndRadius(row, const Radius.circular(3)),
        Paint()..color = const Color(0xEC06131F),
      );
      canvas.drawRect(
        Rect.fromLTWH(row.left, row.top, 2, row.height),
        Paint()..color = unitColor,
      );
      final tp = TextPainter(
        text: TextSpan(
          text:
              '${i + 1}  ${v.unitName}  ${v.distanceMeters.toStringAsFixed(0)} m',
          style: TextStyle(
            color: Colors.white,
            fontSize: 10,
            fontWeight: FontWeight.w600,
          ),
        ),
        textDirection: TextDirection.ltr,
        maxLines: 1,
        ellipsis: '...',
      )..layout(maxWidth: row.width - 12);
      tp.paint(
        canvas,
        Offset(row.left + 7, row.top + (row.height - tp.height) / 2),
      );
    }
  }

  // =========================================================================
  // 4. GPS bearing arrow
  void _drawVolumetric3DArrow(Canvas canvas, Offset center, double scale) {
    final color = relativeBearing.abs() > 90
        ? FmsTheme.amberWarning
        : FmsTheme.emeraldGreen;
    final radians = relativeBearing * math.pi / 180.0;
    final rearAmount = ((relativeBearing.abs() - 80) / 100).clamp(0.0, 1.0);
    final arrowScale = scale * (1 - rearAmount * 0.32);
    const outline = <(double, double)>[
      (0.0, 1.45),
      (0.57, 0.26),
      (0.22, 0.36),
      (0.22, -0.72),
      (-0.22, -0.72),
      (-0.22, 0.36),
      (-0.57, 0.26),
    ];
    Offset point(double x, double z, double height) => _project(
      Vec3(x, height, z).rotateY(radians),
      center,
      arrowScale,
    );
    Path polygon(Iterable<Offset> corners) {
      final path = Path();
      var first = true;
      for (final corner in corners) {
        if (first) {
          path.moveTo(corner.dx, corner.dy);
          first = false;
        } else {
          path.lineTo(corner.dx, corner.dy);
        }
      }
      return path..close();
    }

    final shadow = polygon(outline.map((v) => point(v.$1, v.$2, 0.01)));
    canvas.drawPath(
      shadow.shift(const Offset(0, 4)),
      Paint()
        ..color = const Color(0xB000090F)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 9),
    );
    canvas.drawPath(
      shadow,
      Paint()..color = color.withValues(alpha: 0.18),
    );

    // Extruded walls are drawn before the top so the arrow retains real depth.
    for (var i = 0; i < outline.length; i++) {
      final a = outline[i];
      final b = outline[(i + 1) % outline.length];
      final wall = polygon([
        point(a.$1, a.$2, 0.22),
        point(b.$1, b.$2, 0.22),
        point(b.$1, b.$2, 0.02),
        point(a.$1, a.$2, 0.02),
      ]);
      canvas.drawPath(
        wall,
        Paint()
          ..color = i < 3
              ? Color.lerp(color, const Color(0xFF063C48), 0.60)!
              : Color.lerp(color, const Color(0xFF031B27), 0.78)!,
      );
      canvas.drawPath(
        wall,
        Paint()
          ..color = color.withValues(alpha: 0.30)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 0.8,
      );
    }

    final top = polygon(outline.map((v) => point(v.$1, v.$2, 0.22)));
    canvas.drawPath(top, Paint()..color = color);
    final ridge = point(0, 0.20, 0.32);
    canvas.drawPath(
      polygon([
        point(0, 1.45, 0.22),
        point(-0.57, 0.26, 0.22),
        point(-0.22, 0.36, 0.22),
        ridge,
      ]),
      Paint()..color = Color.lerp(color, Colors.white, 0.48)!,
    );
    canvas.drawPath(
      polygon([
        point(0, 1.45, 0.22),
        ridge,
        point(0.22, 0.36, 0.22),
        point(0.57, 0.26, 0.22),
      ]),
      Paint()..color = Color.lerp(color, const Color(0xFF064C57), 0.44)!,
    );
    canvas.drawPath(
      polygon([
        point(-0.22, 0.36, 0.22),
        ridge,
        point(0.22, 0.36, 0.22),
        point(0.22, -0.72, 0.22),
        point(-0.22, -0.72, 0.22),
      ]),
      Paint()..color = Color.lerp(color, const Color(0xFF12636B), 0.28)!,
    );
    canvas.drawPath(
      top,
      Paint()
        ..color = const Color(0xFFEDFFFF)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.5,
    );
    canvas.drawLine(
      point(0, 1.42, 0.23),
      ridge,
      Paint()
        ..color = Colors.white.withValues(alpha: 0.85)
        ..strokeWidth = 1.3,
    );

    final sweepZ = -0.58 + animPhase * 1.68;
    final sweepHalf = sweepZ > 0.36 ? 0.15 : 0.19;
    canvas.drawLine(
      point(-sweepHalf, sweepZ, 0.235),
      point(sweepHalf, sweepZ, 0.235),
      Paint()
        ..color = Colors.white.withValues(alpha: 0.45)
        ..strokeWidth = 2.0
        ..strokeCap = StrokeCap.round,
    );
  }

  // 5. Cabin reticle
  void _drawCockpitReticle(Canvas canvas, Size size) {
    final reticlePaint = Paint()
      ..color = FmsTheme.cyanAccent.withValues(alpha: 0.35)
      ..strokeWidth = 1.0;

    const bracketLen = 14.0;
    const pad = 10.0;

    canvas.drawLine(
      const Offset(pad, pad),
      const Offset(pad + bracketLen, pad),
      reticlePaint,
    );
    canvas.drawLine(
      const Offset(pad, pad),
      const Offset(pad, pad + bracketLen),
      reticlePaint,
    );

    canvas.drawLine(
      Offset(size.width - pad, pad),
      Offset(size.width - pad - bracketLen, pad),
      reticlePaint,
    );
    canvas.drawLine(
      Offset(size.width - pad, pad),
      Offset(size.width - pad, pad + bracketLen),
      reticlePaint,
    );

    canvas.drawLine(
      Offset(pad, size.height - pad),
      Offset(pad + bracketLen, size.height - pad),
      reticlePaint,
    );
    canvas.drawLine(
      Offset(pad, size.height - pad),
      Offset(pad, size.height - pad - bracketLen),
      reticlePaint,
    );

    canvas.drawLine(
      Offset(size.width - pad, size.height - pad),
      Offset(size.width - pad - bracketLen, size.height - pad),
      reticlePaint,
    );
    canvas.drawLine(
      Offset(size.width - pad, size.height - pad),
      Offset(size.width - pad, size.height - pad - bracketLen),
      reticlePaint,
    );
  }

  @override
  bool shouldRepaint(covariant Nav3dArrowPainter oldDelegate) {
    return oldDelegate.relativeBearing != relativeBearing ||
        oldDelegate.absoluteHeading != absoluteHeading ||
        oldDelegate.targetAzimuth != targetAzimuth ||
        oldDelegate.animPhase != animPhase ||
        oldDelegate.nearbyVehicles != nearbyVehicles;
  }
}

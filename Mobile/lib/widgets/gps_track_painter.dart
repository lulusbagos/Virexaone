import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../models/cabin_bearing.dart';
import '../models/fleet_models.dart';
import '../services/fms_api_service.dart';
import '../theme/fms_theme.dart';

/// Ultra-modern Tactical Cockpit Navigation & Radar Painter
/// Designed for high-contrast clarity in daylight and zero-glare comfort at night.
class GpsTrackPainter extends CustomPainter {
  final List<CabinGpsPoint> track;
  final List<NearbyVehicle> nearbyVehicles;
  final List<RoadSegment> roadSegments;
  final double easting, northing, heading, targetEasting, targetNorthing;
  final String targetName;
  final bool held;
  final double animPhase;

  const GpsTrackPainter({
    required this.track,
    this.nearbyVehicles = const [],
    this.roadSegments = const [],
    required this.easting,
    required this.northing,
    required this.heading,
    required this.targetEasting,
    required this.targetNorthing,
    this.targetName = 'Tujuan',
    required this.held,
    this.animPhase = 0.0,
  });

  @override
  void paint(Canvas canvas, Size size) {
    final bounds = Offset.zero & size;
    canvas.save();
    canvas.clipRect(bounds);

    // Deep high-tech tactical radar cockpit backdrop adaptive to current theme
    canvas.drawColor(FmsTheme.bgDark, BlendMode.src);

    // Vignette background shader
    final bgGlow = Paint()
      ..shader = RadialGradient(
        colors: [
          FmsTheme.surfaceDark.withValues(alpha: 0.70),
          FmsTheme.bgDark.withValues(alpha: 0.92),
          FmsTheme.bgDark,
        ],
        stops: const [0.0, 0.65, 1.0],
      ).createShader(bounds);
    canvas.drawRect(bounds, bgGlow);

    // Origin: 63% down to allow generous forward view ahead of vehicle
    final origin = Offset(size.width * 0.5, size.height * 0.63);
    final span = math.max(200.0, math.min(650.0, size.height * 1.15));
    final pixelsPerMeter = math.min(size.width, size.height) / span;

    Offset project(double east, double north) {
      final offset = CabinBearing.headingUpOffset(
        eastMeters: east - easting,
        northMeters: north - northing,
        headingDeg: heading,
      );
      return Offset(
        origin.dx + offset.$1 * pixelsPerMeter,
        origin.dy - offset.$2 * pixelsPerMeter,
      );
    }

    // --- 1. POLAR RADAR RANGE RINGS & COMPASS TICKS ---
    _drawRadarGrid(canvas, size, origin, pixelsPerMeter);

    // --- 2. REAL GPS BREADCRUMB TRAIL (Riwayat Jalur Nyata) ---
    _drawGpsTrail(canvas, project);

    // --- 3. DYNAMIC NAVIGATION GUIDANCE BEAM & TARGET WAYPOINT ---
    if (targetEasting > 0 && targetNorthing > 0) {
      _drawNavigationRoute(canvas, size, origin, project, pixelsPerMeter);
    }

    // --- 4. SURROUNDING FLEET UNITS & PROXIMITY HAZARD RADAR ---
    _drawSurroundingFleet(canvas, size, origin, pixelsPerMeter);

    // --- 5. OWN VEHICLE COCKPIT AVATAR & HEADLIGHT CONE ---
    _drawOwnVehicle(canvas, origin, pixelsPerMeter);

    // --- 6. HUD OVERLAYS (Scale & Status) ---
    _drawHudScale(canvas, size, pixelsPerMeter);

    canvas.restore();
  }

  // =========================================================================
  // 1. POLAR RADAR GRID & RANGE RINGS
  // =========================================================================
  void _drawRadarGrid(
    Canvas canvas,
    Size size,
    Offset origin,
    double pixelsPerMeter,
  ) {
    final ringPaint = Paint()
      ..color = const Color(0xFF0E3846).withValues(alpha: 0.55)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 0.8;

    final outerRingPaint = Paint()
      ..color = const Color(0xFF00E5FF).withValues(alpha: 0.25)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.2;

    // Range rings at 50m, 100m, 200m, 350m
    final ranges = [50.0, 100.0, 200.0, 350.0];
    for (int i = 0; i < ranges.length; i++) {
      final r = ranges[i] * pixelsPerMeter;
      final isOuter = i == ranges.length - 1;
      canvas.drawCircle(origin, r, isOuter ? outerRingPaint : ringPaint);

      // Distance tag on the right axis
      if (r < size.width * 0.48) {
        final distText = TextPainter(
          text: TextSpan(
            text: '${ranges[i].toInt()}m',
            style: TextStyle(
              color: const Color(0xFF00E5FF).withValues(alpha: 0.50),
              fontSize: 8.5,
              fontWeight: FontWeight.w600,
              letterSpacing: 0.5,
            ),
          ),
          textDirection: TextDirection.ltr,
        )..layout();
        distText.paint(canvas, Offset(origin.dx + r + 3, origin.dy - 6));
      }
    }

    // 8-Spoke Radial Sector Lines
    final spokePaint = Paint()
      ..color = const Color(0xFF0A2B36).withValues(alpha: 0.50)
      ..strokeWidth = 0.7;

    for (int deg = 0; deg < 360; deg += 45) {
      final rad = deg * math.pi / 180.0;
      final maxR = ranges.last * pixelsPerMeter * 1.1;
      final pt = origin + Offset(math.sin(rad) * maxR, -math.cos(rad) * maxR);
      canvas.drawLine(origin, pt, spokePaint);
    }

    // Rotating Cardinal Compass Ticks (Heading-Up)
    // North is at angle = -heading
    final northRad = (-heading) * math.pi / 180.0;
    final outerR = ranges.last * pixelsPerMeter;
    final northPt = origin + Offset(math.sin(northRad) * outerR, -math.cos(northRad) * outerR);

    // North Indicator Arrow
    final nPaint = Paint()..color = const Color(0xFFFF5252);
    final nPath = Path()
      ..moveTo(northPt.dx, northPt.dy)
      ..lineTo(
        northPt.dx + math.sin(northRad + 2.5) * 10,
        northPt.dy - math.cos(northRad + 2.5) * 10,
      )
      ..lineTo(
        northPt.dx + math.sin(northRad - 2.5) * 10,
        northPt.dy - math.cos(northRad - 2.5) * 10,
      )
      ..close();
    canvas.drawPath(nPath, nPaint);

    final nLabel = TextPainter(
      text: const TextSpan(
        text: 'N',
        style: TextStyle(
          color: Color(0xFFFF5252),
          fontSize: 9.5,
          fontWeight: FontWeight.w900,
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    nLabel.paint(
      canvas,
      Offset(
        northPt.dx + math.sin(northRad) * 14 - nLabel.width / 2,
        northPt.dy - math.cos(northRad) * 14 - nLabel.height / 2,
      ),
    );
  }

  // =========================================================================
  // 2. GPS BREADCRUMB TRAIL (Smooth Real Physical Path)
  // =========================================================================
  void _drawGpsTrail(Canvas canvas, Offset Function(double, double) project) {
    if (track.length < 2) return;

    final trailPath = Path();
    bool hasMoved = false;

    for (int i = 1; i < track.length; i++) {
      final before = track[i - 1];
      final after = track[i];
      final dx = after.easting - before.easting;
      final dy = after.northing - before.northing;
      final gapSeconds = after.recordedAt.difference(before.recordedAt).inSeconds;
      final maxPlausibleMeters = math.max(30, gapSeconds * 22);

      // Skip invalid teleportation glitches
      if (gapSeconds <= 0 ||
          gapSeconds > 90 ||
          dx * dx + dy * dy > maxPlausibleMeters * maxPlausibleMeters) {
        continue;
      }

      final p1 = project(before.easting, before.northing);
      final p2 = project(after.easting, after.northing);

      if (!hasMoved) {
        trailPath.moveTo(p1.dx, p1.dy);
        hasMoved = true;
      }
      trailPath.lineTo(p2.dx, p2.dy);
    }

    if (hasMoved) {
      // Outer glow
      canvas.drawPath(
        trailPath,
        Paint()
          ..color = (held ? const Color(0xFFE5A93C) : const Color(0xFF00FFA3))
              .withValues(alpha: 0.25)
          ..strokeWidth = 6.0
          ..style = PaintingStyle.stroke
          ..strokeCap = StrokeCap.round
          ..strokeJoin = StrokeJoin.round,
      );

      // Core crisp path
      canvas.drawPath(
        trailPath,
        Paint()
          ..color = held ? const Color(0xFFFFB84D) : const Color(0xFF00FFA3)
          ..strokeWidth = 2.4
          ..style = PaintingStyle.stroke
          ..strokeCap = StrokeCap.round
          ..strokeJoin = StrokeJoin.round,
      );
    }

    // Historical trail dots
    final dotPaint = Paint()
      ..color = (held ? const Color(0xFFFFD180) : const Color(0xFF80FFD4))
          .withValues(alpha: 0.70);
    for (int i = 0; i < track.length; i += 2) {
      final pt = project(track[i].easting, track[i].northing);
      canvas.drawCircle(pt, 1.8, dotPaint);
    }
  }

  // =========================================================================
  // 3. DYNAMIC NAVIGATION ROUTE & TARGET BEACON
  // =========================================================================
  void _drawNavigationRoute(
    Canvas canvas,
    Size size,
    Offset origin,
    Offset Function(double, double) project,
    double pixelsPerMeter,
  ) {
    final targetPt = project(targetEasting, targetNorthing);
    final routeVec = targetPt - origin;
    final routeDist = routeVec.distance;
    final targetColor = held ? const Color(0xFFFFB84D) : const Color(0xFF00E5FF);

    // Glowing Neon Navigation Beam
    if (routeDist > 10) {
      final dir = routeVec / routeDist;

      // Outer wide glow beam
      canvas.drawLine(
        origin,
        targetPt,
        Paint()
          ..color = targetColor.withValues(alpha: 0.18)
          ..strokeWidth = 6.0
          ..strokeCap = StrokeCap.round,
      );

      // Dashed navigation line with animated pulse
      final dashPaint = Paint()
        ..color = targetColor.withValues(alpha: 0.85)
        ..strokeWidth = 2.0
        ..strokeCap = StrokeCap.round;

      const dashLen = 10.0;
      const gapLen = 7.0;
      final animOffset = (animPhase * (dashLen + gapLen)) % (dashLen + gapLen);

      for (double d = animOffset; d < routeDist; d += (dashLen + gapLen)) {
        final p1 = origin + dir * d;
        final p2 = origin + dir * math.min(d + dashLen, routeDist);
        canvas.drawLine(p1, p2, dashPaint);
      }

      // Traveling Chevron Guidance Arrows
      final chevronPaint = Paint()
        ..color = targetColor
        ..strokeWidth = 2.0
        ..style = PaintingStyle.stroke
        ..strokeCap = StrokeCap.round;

      for (double d = 35.0; d < routeDist - 25.0; d += 65.0) {
        final cp = origin + dir * d;
        final normal = Offset(-dir.dy, dir.dx);
        final chPath = Path()
          ..moveTo(cp.dx - normal.dx * 6 - dir.dx * 6, cp.dy - normal.dy * 6 - dir.dy * 6)
          ..lineTo(cp.dx, cp.dy)
          ..lineTo(cp.dx + normal.dx * 6 - dir.dx * 6, cp.dy + normal.dy * 6 - dir.dy * 6);
        canvas.drawPath(chPath, chevronPaint);
      }
    }

    // Calculate real geographic distance
    final realDist = math.sqrt(
      math.pow(targetEasting - easting, 2) + math.pow(targetNorthing - northing, 2),
    );
    final distLabel = realDist >= 1000
        ? '${(realDist / 1000).toStringAsFixed(2)} km'
        : '${realDist.round()} m';

    // Check if target is inside screen viewport
    final safeMargin = 28.0;
    final safeRect = Rect.fromLTRB(
      safeMargin,
      safeMargin,
      size.width - safeMargin,
      size.height - safeMargin,
    );

    if (safeRect.contains(targetPt)) {
      // --- A. ONSCREEN TARGET LANDING BEACON ---
      _drawOnscreenTargetBeacon(canvas, targetPt, targetColor, distLabel);
    } else {
      // --- B. OFFSCREEN EDGE-CLAMPED WAYPOINT HUD ---
      _drawOffscreenTargetHud(canvas, size, origin, targetPt, safeRect, targetColor, distLabel);
    }
  }

  void _drawOnscreenTargetBeacon(
    Canvas canvas,
    Offset point,
    Color color,
    String distLabel,
  ) {
    // Concentric pulsing radar waves
    canvas.drawCircle(
      point,
      22.0,
      Paint()..color = color.withValues(alpha: 0.12),
    );
    canvas.drawCircle(
      point,
      14.0,
      Paint()
        ..color = color.withValues(alpha: 0.40)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.4,
    );

    // Target Diamond Core
    final core = Path()
      ..moveTo(point.dx, point.dy - 8)
      ..lineTo(point.dx + 8, point.dy)
      ..lineTo(point.dx, point.dy + 8)
      ..lineTo(point.dx - 8, point.dy)
      ..close();
    canvas.drawPath(core, Paint()..color = color);
    canvas.drawCircle(point, 2.5, Paint()..color = const Color(0xFF030910));

    // Target Badge
    _drawTargetBadge(canvas, point, color, targetName, distLabel, false);
  }

  void _drawOffscreenTargetHud(
    Canvas canvas,
    Size size,
    Offset origin,
    Offset targetPt,
    Rect safeRect,
    Color color,
    String distLabel,
  ) {
    final dx = targetPt.dx - origin.dx;
    final dy = targetPt.dy - origin.dy;
    double fraction = 1.0;
    if (dx > 0) fraction = math.min(fraction, (safeRect.right - origin.dx) / dx);
    if (dx < 0) fraction = math.min(fraction, (safeRect.left - origin.dx) / dx);
    if (dy > 0) fraction = math.min(fraction, (safeRect.bottom - origin.dy) / dy);
    if (dy < 0) fraction = math.min(fraction, (safeRect.top - origin.dy) / dy);
    fraction = fraction.clamp(0.0, 1.0);

    final edgeMarker = Offset(origin.dx + dx * fraction, origin.dy + dy * fraction);

    // Bearing Pointer towards target
    final dir = (targetPt - origin) / ((targetPt - origin).distance + 0.001);
    final normal = Offset(-dir.dy, dir.dx);

    final arrowPath = Path()
      ..moveTo(edgeMarker.dx + dir.dx * 12, edgeMarker.dy + dir.dy * 12)
      ..lineTo(edgeMarker.dx - dir.dx * 4 + normal.dx * 7, edgeMarker.dy - dir.dy * 4 + normal.dy * 7)
      ..lineTo(edgeMarker.dx, edgeMarker.dy)
      ..lineTo(edgeMarker.dx - dir.dx * 4 - normal.dx * 7, edgeMarker.dy - dir.dy * 4 - normal.dy * 7)
      ..close();

    canvas.drawPath(arrowPath, Paint()..color = color);
    canvas.drawPath(
      arrowPath,
      Paint()
        ..color = const Color(0xFF030910)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.0,
    );

    _drawTargetBadge(canvas, edgeMarker, color, targetName, distLabel, true);
  }

  void _drawTargetBadge(
    Canvas canvas,
    Offset marker,
    Color color,
    String name,
    String distance,
    bool isOffscreen,
  ) {
    final title = TextPainter(
      text: TextSpan(
        text: name,
        style: const TextStyle(
          color: Color(0xFFF1FAFA),
          fontSize: 10.5,
          fontWeight: FontWeight.w800,
          letterSpacing: 0.3,
        ),
      ),
      textDirection: TextDirection.ltr,
      maxLines: 1,
      ellipsis: '...',
    )..layout(maxWidth: 130);

    final subtitle = TextPainter(
      text: TextSpan(
        text: '${isOffscreen ? 'ARAH TUJUAN' : 'TITIK TARGET'} • $distance',
        style: TextStyle(
          color: color,
          fontSize: 8.5,
          fontWeight: FontWeight.w700,
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();

    final badgeW = math.max(title.width, subtitle.width) + 16;
    const badgeH = 34.0;

    double bx = marker.dx + 12;
    if (bx + badgeW > 350) bx = marker.dx - badgeW - 12;
    double by = (marker.dy - badgeH / 2).clamp(24.0, 400.0);

    final badgeRect = Rect.fromLTWH(bx, by, badgeW, badgeH);
    canvas.drawRRect(
      RRect.fromRectAndRadius(badgeRect, const Radius.circular(5)),
      Paint()..color = const Color(0xF005161F),
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(badgeRect, const Radius.circular(5)),
      Paint()
        ..color = color.withValues(alpha: 0.80)
        ..strokeWidth = 1.2
        ..style = PaintingStyle.stroke,
    );

    title.paint(canvas, Offset(badgeRect.left + 8, badgeRect.top + 3));
    subtitle.paint(canvas, Offset(badgeRect.left + 8, badgeRect.top + 18));
  }

  // =========================================================================
  // 4. SURROUNDING FLEET UNITS & PROXIMITY HAZARD RADAR
  // =========================================================================
  void _drawSurroundingFleet(
    Canvas canvas,
    Size size,
    Offset origin,
    double pixelsPerMeter,
  ) {
    if (held || nearbyVehicles.isEmpty) return;

    final placedLabels = <Rect>[];
    final visibleBounds = Rect.fromLTRB(14, 28, size.width - 14, size.height - 28);

    for (final unit in nearbyVehicles) {
      final point = origin + Offset(
        unit.lateralOffsetMeters * pixelsPerMeter,
        -unit.forwardOffsetMeters * pixelsPerMeter,
      );

      // Skip if off screen or right on top of truck center
      if (!visibleBounds.contains(point) || (point - origin).distance < 14) {
        continue;
      }

      // Category styling & colors
      Color unitColor;
      switch (unit.unitType) {
        case 'EX':
          unitColor = const Color(0xFFFF8C00); // Orange
          break;
        case 'HD':
          unitColor = const Color(0xFF00E5FF); // Cyber Cyan
          break;
        case 'DZ':
          unitColor = const Color(0xFFFFB300); // Amber Dozer
          break;
        case 'LV':
          unitColor = const Color(0xFF00FFA3); // Emerald Light Vehicle
          break;
        default:
          unitColor = const Color(0xFF90CAF9); // Blue
      }

      final isCollisionAlert = unit.isCollisionWarning || unit.distanceMeters < 50.0;
      if (isCollisionAlert) {
        unitColor = const Color(0xFFFF3366); // Neon Red/Pink Danger
      }

      // --- A. PROXIMITY COLLISION WARNING RING (<50m) ---
      if (isCollisionAlert) {
        // Pulsing hazard bubble
        canvas.drawCircle(
          point,
          18.0,
          Paint()..color = const Color(0x33FF3366),
        );
        canvas.drawCircle(
          point,
          18.0,
          Paint()
            ..color = const Color(0xFFFF3366)
            ..strokeWidth = 1.2
            ..style = PaintingStyle.stroke,
        );
      } else {
        // Subtle detection aura
        canvas.drawCircle(
          point,
          12.0,
          Paint()..color = unitColor.withValues(alpha: 0.16),
        );
      }

      // --- B. UNIT TACTICAL SYMBOL ---
      if (unit.unitType == 'EX') {
        // Diamond for Excavator
        final dPath = Path()
          ..moveTo(point.dx, point.dy - 6)
          ..lineTo(point.dx + 6, point.dy)
          ..lineTo(point.dx, point.dy + 6)
          ..lineTo(point.dx - 6, point.dy)
          ..close();
        canvas.drawPath(dPath, Paint()..color = unitColor);
        canvas.drawPath(
          dPath,
          Paint()
            ..color = const Color(0xFF030910)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1.0,
        );
      } else if (unit.unitType == 'HD') {
        // Rounded box for Hauler
        final hRect = RRect.fromRectAndRadius(
          Rect.fromCenter(center: point, width: 11, height: 11),
          const Radius.circular(2.5),
        );
        canvas.drawRRect(hRect, Paint()..color = unitColor);
        canvas.drawRRect(
          hRect,
          Paint()
            ..color = const Color(0xFF030910)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1.0,
        );
      } else {
        // Circle for others
        canvas.drawCircle(point, 5.0, Paint()..color = unitColor);
        canvas.drawCircle(
          point,
          5.0,
          Paint()
            ..color = const Color(0xFF030910)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1.0,
        );
      }

      // --- C. UNIT CALLOUT TAG ---
      final distText = unit.distanceMeters >= 1000
          ? '${(unit.distanceMeters / 1000).toStringAsFixed(1)}k'
          : '${unit.distanceMeters.round()}m';

      final label = TextPainter(
        text: TextSpan(
          children: [
            if (isCollisionAlert)
              const TextSpan(
                text: '⚠️ ',
                style: TextStyle(fontSize: 9.0),
              ),
            TextSpan(
              text: '${unit.unitName} ',
              style: TextStyle(
                color: unitColor,
                fontSize: 9.5,
                fontWeight: FontWeight.w900,
                letterSpacing: 0.2,
              ),
            ),
            TextSpan(
              text: distText,
              style: const TextStyle(
                color: Color(0xFFE0F2F1),
                fontSize: 8.5,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
        textDirection: TextDirection.ltr,
        maxLines: 1,
      )..layout(maxWidth: 120);

      final labelW = label.width + 10;
      final labelH = label.height + 6;

      double lx = point.dx + 9;
      if (lx + labelW > size.width - 8) {
        lx = point.dx - 9 - labelW;
      }
      double ly = (point.dy - labelH / 2).clamp(24.0, size.height - labelH - 20);

      final labelRect = Rect.fromLTWH(lx, ly, labelW, labelH);

      // Prevent overlapping labels
      if (placedLabels.any((r) => r.overlaps(labelRect))) {
        ly = point.dy + 8;
      }
      placedLabels.add(Rect.fromLTWH(lx, ly, labelW, labelH));

      canvas.drawRRect(
        RRect.fromRectAndRadius(labelRect, const Radius.circular(4)),
        Paint()..color = isCollisionAlert ? const Color(0xF52A0A14) : const Color(0xF2071922),
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(labelRect, const Radius.circular(4)),
        Paint()
          ..color = unitColor.withValues(alpha: 0.75)
          ..strokeWidth = 1.0
          ..style = PaintingStyle.stroke,
      );

      label.paint(canvas, Offset(labelRect.left + 5, labelRect.top + 3));
    }
  }

  // =========================================================================
  // 5. OWN VEHICLE COCKPIT AVATAR & HEADLIGHT ILLUMINATION
  // =========================================================================
  void _drawOwnVehicle(Canvas canvas, Offset origin, double pixelsPerMeter) {
    final truckColor = held ? const Color(0xFFFFB84D) : const Color(0xFF00FFA3);

    // Headlight Illuminating Spotlight Cone (Forward 40m)
    final conePath = Path()
      ..moveTo(origin.dx, origin.dy)
      ..lineTo(origin.dx - 32, origin.dy - 65)
      ..lineTo(origin.dx + 32, origin.dy - 65)
      ..close();

    final coneGradient = Paint()
      ..shader = LinearGradient(
        begin: Alignment.bottomCenter,
        end: Alignment.topCenter,
        colors: [
          truckColor.withValues(alpha: 0.22),
          truckColor.withValues(alpha: 0.04),
          Colors.transparent,
        ],
      ).createShader(Rect.fromLTWH(origin.dx - 32, origin.dy - 65, 64, 65));
    canvas.drawPath(conePath, coneGradient);

    // Safety Bubble Radius (15m buffer around vehicle)
    canvas.drawCircle(
      origin,
      15 * pixelsPerMeter,
      Paint()
        ..color = truckColor.withValues(alpha: 0.12)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.0,
    );

    // Forward Direction Beam Pointer
    canvas.drawLine(
      origin,
      Offset(origin.dx, origin.dy - 22),
      Paint()
        ..color = truckColor
        ..strokeWidth = 2.0
        ..strokeCap = StrokeCap.round,
    );

    // Heavy Equipment Chassis Silhouette
    final truckBody = RRect.fromRectAndRadius(
      Rect.fromCenter(center: origin, width: 17, height: 26),
      const Radius.circular(3.5),
    );
    canvas.drawRRect(truckBody, Paint()..color = truckColor);
    canvas.drawRRect(
      truckBody,
      Paint()
        ..color = const Color(0xFFFFFFFF)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.2,
    );

    // Cab / Windshield Glass
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromCenter(center: origin.translate(0, -5), width: 12, height: 6),
        const Radius.circular(1.5),
      ),
      Paint()..color = const Color(0xFF06222B),
    );

    // Wheel Tires
    final wheelPaint = Paint()..color = const Color(0xFF102830);
    for (final side in [-1.0, 1.0]) {
      for (final offset in [-7.0, 7.0]) {
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            Rect.fromCenter(
              center: origin.translate(side * 10, offset),
              width: 4,
              height: 7,
            ),
            const Radius.circular(1),
          ),
          wheelPaint,
        );
      }
    }
  }

  // =========================================================================
  // 6. HUD SCALE & RANGE STATUS
  // =========================================================================
  void _drawHudScale(Canvas canvas, Size size, double pixelsPerMeter) {
    // 50m Scale Ruler
    final fiftyM = 50.0 * pixelsPerMeter;
    final scalePaint = Paint()
      ..color = const Color(0xFF00E5FF).withValues(alpha: 0.70)
      ..strokeWidth = 1.5;

    final startX = 14.0;
    final bottomY = size.height - 12.0;

    canvas.drawLine(Offset(startX, bottomY), Offset(startX + fiftyM, bottomY), scalePaint);
    canvas.drawLine(Offset(startX, bottomY - 4), Offset(startX, bottomY + 4), scalePaint);
    canvas.drawLine(Offset(startX + fiftyM, bottomY - 4), Offset(startX + fiftyM, bottomY + 4), scalePaint);

    final scaleText = TextPainter(
      text: const TextSpan(
        text: '50m',
        style: TextStyle(
          color: Color(0xFF80DEEA),
          fontSize: 8.5,
          fontWeight: FontWeight.w700,
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    scaleText.paint(canvas, Offset(startX + fiftyM + 6, bottomY - 6));
  }

  @override
  bool shouldRepaint(covariant GpsTrackPainter oldDelegate) =>
      oldDelegate.track != track ||
      oldDelegate.nearbyVehicles != nearbyVehicles ||
      oldDelegate.roadSegments != roadSegments ||
      oldDelegate.easting != easting ||
      oldDelegate.northing != northing ||
      oldDelegate.heading != heading ||
      oldDelegate.targetEasting != targetEasting ||
      oldDelegate.targetNorthing != targetNorthing ||
      oldDelegate.targetName != targetName ||
      oldDelegate.held != held ||
      oldDelegate.animPhase != animPhase;
}

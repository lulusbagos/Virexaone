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
  final bool isDanger;

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
    this.isDanger = false,
  });

  @override
  void paint(Canvas canvas, Size size) {
    final bounds = Offset.zero & size;
    canvas.save();
    canvas.clipRect(bounds);

    // Deep high-tech tactical radar cockpit backdrop adaptive to current theme
    canvas.drawColor(FmsTheme.bgDark, BlendMode.src);

    // Vignette background shader (with danger ambient hue if active)
    final bgGlow = Paint()
      ..shader = RadialGradient(
        colors: isDanger
            ? [
                const Color(0x35FF1744),
                FmsTheme.bgDark.withValues(alpha: 0.94),
                FmsTheme.bgDark,
              ]
            : [
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

    // --- 1B. ULTRA-THIN TACTICAL RADAR SWEEP (Jarum Radar Tipis Presisi) ---
    _drawThinRadarSweep(canvas, origin, 350.0 * pixelsPerMeter);

    // --- 2. REAL GPS BREADCRUMB TRAIL (Riwayat Jalur Nyata yang Halus) ---
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
    final ringColor = isDanger ? const Color(0xFF4A101D) : const Color(0xFF0E3846);
    final ringPaint = Paint()
      ..color = ringColor.withValues(alpha: isDanger ? 0.70 : 0.55)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 0.6;

    final outerRingColor = isDanger ? const Color(0xFFFF3366) : const Color(0xFF00E5FF);
    final outerRingPaint = Paint()
      ..color = outerRingColor.withValues(alpha: isDanger ? 0.65 : 0.25)
      ..style = PaintingStyle.stroke
      ..strokeWidth = isDanger ? 1.4 : 1.0;

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
              color: outerRingColor.withValues(alpha: isDanger ? 0.75 : 0.50),
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
  // 1B. ULTRA-THIN TACTICAL RADAR SWEEP (Jarum Radar Super Tipis Presisi Tinggi)
  // =========================================================================
  void _drawThinRadarSweep(Canvas canvas, Offset origin, double maxRadius) {
    // 360-degree rotation based on animPhase (0.0 -> 1.0)
    final sweepAngle = (animPhase * 2 * math.pi) - (math.pi / 2);
    const trailAngle = math.pi / 14.0; // Ramping ~13 derajat agar jarum tampak tipis, tajam & elegan
    const numFanSteps = 16;

    // Sweep menjangkau seluruh grid radar hingga ring terluar
    final radius = maxRadius;
    final sweepColor = isDanger ? const Color(0xFFFF3366) : const Color(0xFF00E5FF);

    // 1. Ethereal Phosphor Glow Fan (Kabut ekor fosfor halus)
    for (int i = 0; i < numFanSteps; i++) {
      final f1 = i / numFanSteps;
      final f2 = (i + 1) / numFanSteps;
      final a1 = sweepAngle - trailAngle * (1.0 - f1);
      final a2 = sweepAngle - trailAngle * (1.0 - f2);
      final alpha = (math.pow(f2, 2.6) * 0.12).clamp(0.0, 0.12);

      final fanPath = Path()
        ..moveTo(origin.dx, origin.dy)
        ..lineTo(origin.dx + math.cos(a1) * radius, origin.dy + math.sin(a1) * radius)
        ..lineTo(origin.dx + math.cos(a2) * radius, origin.dy + math.sin(a2) * radius)
        ..close();

      canvas.drawPath(
        fanPath,
        Paint()
          ..color = sweepColor.withValues(alpha: alpha)
          ..style = PaintingStyle.fill,
      );
    }

    // 2. Needle Beam Tip Endpoint
    final tip = Offset(
      origin.dx + math.cos(sweepAngle) * radius,
      origin.dy + math.sin(sweepAngle) * radius,
    );

    // 3. Delicate Luminous Glow Halo (1.8px)
    canvas.drawLine(
      origin,
      tip,
      Paint()
        ..color = sweepColor.withValues(alpha: 0.35)
        ..strokeWidth = 1.8
        ..strokeCap = StrokeCap.round,
    );

    // 4. Ultra-Fine Razor Needle Core (jarum radar 1.0px super tajam & presisi)
    canvas.drawLine(
      origin,
      tip,
      Paint()
        ..color = Colors.white.withValues(alpha: 0.95)
        ..strokeWidth = 1.0
        ..strokeCap = StrokeCap.round,
    );

    // 5. Micro Tip Blip Dot (1.5px)
    canvas.drawCircle(
      tip,
      1.5,
      Paint()..color = sweepColor,
    );
    canvas.drawCircle(
      tip,
      0.8,
      Paint()..color = Colors.white,
    );

    // 6. Center Reticle Glow Dot
    canvas.drawCircle(
      origin,
      1.8,
      Paint()..color = sweepColor.withValues(alpha: 0.85),
    );
  }

  // =========================================================================
  // 2. GPS BREADCRUMB TRAIL (Tactical Cyan with Progressive Age Fade)
  // =========================================================================
  void _drawGpsTrail(Canvas canvas, Offset Function(double, double) project) {
    if (track.length < 2) return;

    final n = track.length;
    final baseColor = held ? const Color(0xFFFFB300) : const Color(0xFF00E5FF);

    for (int i = 1; i < n; i++) {
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

      // Progressive age factor: 0.0 (oldest) to 1.0 (most recent)
      final progress = i / n;
      final alpha = (0.05 + progress * 0.45).clamp(0.04, 0.50);

      // Soft glow on more recent segments
      if (progress > 0.55) {
        canvas.drawLine(
          p1,
          p2,
          Paint()
            ..color = baseColor.withValues(alpha: alpha * 0.35)
            ..strokeWidth = 3.2
            ..strokeCap = StrokeCap.round,
        );
      }

      // Crisp refined line segment (tipis 1.2px)
      canvas.drawLine(
        p1,
        p2,
        Paint()
          ..color = baseColor.withValues(alpha: alpha)
          ..strokeWidth = 1.2
          ..strokeCap = StrokeCap.round,
      );

      // Subtle breadcrumb dot at intervals
      if (i % 2 == 0) {
        canvas.drawCircle(
          p2,
          1.2,
          Paint()..color = baseColor.withValues(alpha: alpha * 0.75),
        );
      }
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
  // 4. SURROUNDING FLEET UNITS & REAL PHOSPHOR RADAR DECAY BLIP
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

    // Current radar sweep needle angle (0 = East, pi/2 = South, pi = West, -pi/2 = North)
    final sweepAngle = (animPhase * 2 * math.pi) - (math.pi / 2);
    double normalizeAngle(double a) {
      a = a % (2 * math.pi);
      if (a < 0) a += 2 * math.pi;
      return a;
    }
    final normSweep = normalizeAngle(sweepAngle);

    // Real PPI Radar phosphor persistence (~225 degrees = ~3.1 detik dari putaran 5 detik)
    const decayAngle = math.pi * 1.25;

    for (final unit in nearbyVehicles) {
      final point = origin + Offset(
        unit.lateralOffsetMeters * pixelsPerMeter,
        -unit.forwardOffsetMeters * pixelsPerMeter,
      );

      // Skip if off screen or right on top of truck center
      if (!visibleBounds.contains(point) || (point - origin).distance < 14) {
        continue;
      }

      final isCollisionAlert = unit.isCollisionWarning || unit.distanceMeters < 50.0;

      // Calculate unit angle from origin in screen coordinates
      final dx = point.dx - origin.dx;
      final dy = point.dy - origin.dy;
      final unitAngle = math.atan2(dy, dx);
      final normUnit = normalizeAngle(unitAngle);

      // Clockwise angular distance from sweep line to unit:
      double anglePast = normSweep - normUnit;
      if (anglePast < 0) anglePast += 2 * math.pi;

      // Phosphor decay calculation:
      // Saat tersapu: Ping flash terang benderang
      // Paruh awal (~1.6 detik): Tetap 100% terang & jelas terbaca oleh operator
      // Paruh akhir: Memudar secara halus dan bertahap
      // Di luar jendela: Menghilang sejenak hingga putaran jarum berikutnya
      double phosphorAlpha = 0.0;
      if (anglePast < decayAngle) {
        final progress = anglePast / decayAngle;
        if (progress < 0.50) {
          phosphorAlpha = 1.0;
        } else {
          final fadeProgress = (progress - 0.50) / 0.50;
          phosphorAlpha = math.pow(1.0 - fadeProgress, 1.8).toDouble();
        }
      }

      // Safety critical: Collision alert (<50m) keeps a pulsing warning silhouette even in the dark
      final double effectiveAlpha;
      if (isCollisionAlert) {
        final pulseBase = 0.35 + 0.15 * math.sin(animPhase * 8 * math.pi).abs();
        effectiveAlpha = math.max(pulseBase, phosphorAlpha);
      } else {
        effectiveAlpha = phosphorAlpha;
      }

      // If unit has faded out completely, skip drawing to achieve authentic radar stealth effect
      if (effectiveAlpha < 0.02) {
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

      if (isCollisionAlert) {
        unitColor = const Color(0xFFFF3366); // Neon Red/Pink Danger
      }

      // --- 0. RADAR PING IMPACT FLASH (When needle line sweeps over unit) ---
      if (anglePast < 0.12) {
        final pingProgress = anglePast / 0.12; // 0.0 -> 1.0
        final pingRadius = 6.0 + pingProgress * 15.0;
        final pingAlpha = ((1.0 - pingProgress) * 0.75).clamp(0.0, 1.0);
        canvas.drawCircle(
          point,
          pingRadius,
          Paint()
            ..color = (isCollisionAlert ? const Color(0xFFFF3366) : unitColor).withValues(alpha: pingAlpha)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1.2,
        );

        if (anglePast < 0.05) {
          // Instant hot center dot on contact
          canvas.drawCircle(
            point,
            2.5,
            Paint()..color = Colors.white.withValues(alpha: (1.0 - anglePast / 0.05) * 0.9),
          );
        }
      }

      // --- A. PROXIMITY COLLISION WARNING RING (<50m) ---
      if (isCollisionAlert) {
        // Pulsing hazard bubble
        canvas.drawCircle(
          point,
          18.0,
          Paint()..color = const Color(0x33FF3366).withValues(alpha: 0.25 * effectiveAlpha),
        );
        canvas.drawCircle(
          point,
          18.0,
          Paint()
            ..color = const Color(0xFFFF3366).withValues(alpha: 0.95 * effectiveAlpha)
            ..strokeWidth = 1.3
            ..style = PaintingStyle.stroke,
        );
      } else {
        // Subtle detection aura
        canvas.drawCircle(
          point,
          12.0,
          Paint()..color = unitColor.withValues(alpha: 0.18 * effectiveAlpha),
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
        canvas.drawPath(dPath, Paint()..color = unitColor.withValues(alpha: effectiveAlpha));
        canvas.drawPath(
          dPath,
          Paint()
            ..color = const Color(0xFF030910).withValues(alpha: effectiveAlpha)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1.0,
        );
      } else if (unit.unitType == 'HD') {
        // Rounded box for Hauler
        final hRect = RRect.fromRectAndRadius(
          Rect.fromCenter(center: point, width: 11, height: 11),
          const Radius.circular(2.5),
        );
        canvas.drawRRect(hRect, Paint()..color = unitColor.withValues(alpha: effectiveAlpha));
        canvas.drawRRect(
          hRect,
          Paint()
            ..color = const Color(0xFF030910).withValues(alpha: effectiveAlpha)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1.0,
        );
      } else {
        // Circle for others
        canvas.drawCircle(point, 5.0, Paint()..color = unitColor.withValues(alpha: effectiveAlpha));
        canvas.drawCircle(
          point,
          5.0,
          Paint()
            ..color = const Color(0xFF030910).withValues(alpha: effectiveAlpha)
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
              TextSpan(
                text: '⚠️ ',
                style: TextStyle(
                  fontSize: 9.0,
                  color: const Color(0xFFFF3366).withValues(alpha: effectiveAlpha),
                ),
              ),
            TextSpan(
              text: '${unit.unitName} ',
              style: TextStyle(
                color: unitColor.withValues(alpha: effectiveAlpha),
                fontSize: 9.5,
                fontWeight: FontWeight.w900,
                letterSpacing: 0.2,
              ),
            ),
            TextSpan(
              text: distText,
              style: TextStyle(
                color: const Color(0xFFE0F2F1).withValues(alpha: effectiveAlpha * 0.90),
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
        Paint()
          ..color = (isCollisionAlert ? const Color(0xF52A0A14) : const Color(0xF2071922))
              .withValues(alpha: 0.94 * effectiveAlpha),
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(labelRect, const Radius.circular(4)),
        Paint()
          ..color = (isCollisionAlert ? const Color(0xFFFF3366) : unitColor)
              .withValues(alpha: 0.80 * effectiveAlpha)
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
    final truckColor = held ? const Color(0xFFFFB84D) : const Color(0xFF00E5FF);

    // 1. Dynamic Xenon Twin Headlight Cones (Forward High-Beam Spotlight)
    final conePath = Path()
      ..moveTo(origin.dx - 5, origin.dy - 12)
      ..lineTo(origin.dx - 38, origin.dy - 85)
      ..lineTo(origin.dx + 38, origin.dy - 85)
      ..lineTo(origin.dx + 5, origin.dy - 12)
      ..close();

    final coneGradient = Paint()
      ..shader = LinearGradient(
        begin: Alignment.bottomCenter,
        end: Alignment.topCenter,
        colors: [
          (held ? const Color(0xFFFFD180) : const Color(0xFF00E5FF)).withValues(alpha: 0.28),
          (held ? const Color(0xFFFFE082) : const Color(0xFF80D8FF)).withValues(alpha: 0.08),
          Colors.transparent,
        ],
      ).createShader(Rect.fromLTWH(origin.dx - 38, origin.dy - 85, 76, 85));
    canvas.drawPath(conePath, coneGradient);

    // Twin High-Intensity Laser Beams
    for (final side in [-6.0, 6.0]) {
      canvas.drawLine(
        Offset(origin.dx + side, origin.dy - 12),
        Offset(origin.dx + side * 2.8, origin.dy - 65),
        Paint()
          ..color = Colors.white.withValues(alpha: 0.75)
          ..strokeWidth = 1.3
          ..strokeCap = StrokeCap.round,
      );
    }

    // 2. Dynamic Radar Scan Wave Propagation (Pulsing Halo Ring)
    final pulseR = (10.0 + 22.0 * animPhase) * pixelsPerMeter.clamp(0.8, 1.8);
    canvas.drawCircle(
      origin,
      pulseR,
      Paint()
        ..color = truckColor.withValues(alpha: (1.0 - animPhase) * 0.35)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.2,
    );

    // Safety Buffer Zone (15m Circle)
    canvas.drawCircle(
      origin,
      15 * pixelsPerMeter,
      Paint()
        ..color = truckColor.withValues(alpha: 0.12)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.9,
    );

    // 3. Direction Vector Pointer with Neon Arrowhead
    canvas.drawLine(
      origin.translate(0, -14),
      origin.translate(0, -32),
      Paint()
        ..color = truckColor
        ..strokeWidth = 2.0
        ..strokeCap = StrokeCap.round,
    );
    final tipPath = Path()
      ..moveTo(origin.dx, origin.dy - 36)
      ..lineTo(origin.dx - 4.5, origin.dy - 28)
      ..lineTo(origin.dx + 4.5, origin.dy - 28)
      ..close();
    canvas.drawPath(tipPath, Paint()..color = truckColor);

    // 4. Heavy Mining Dump Truck Wheels (6 Large Lugged Tires)
    final tirePaint = Paint()..color = const Color(0xFF07141E);
    final rimPaint = Paint()..color = const Color(0xFF00E5FF).withValues(alpha: 0.75);
    for (final side in [-1.0, 1.0]) {
      // Front Steer Tires
      final fTire = RRect.fromRectAndRadius(
        Rect.fromCenter(center: origin.translate(side * 11.5, -7), width: 4.8, height: 9.5),
        const Radius.circular(1.5),
      );
      canvas.drawRRect(fTire, tirePaint);
      canvas.drawRRect(fTire, Paint()..color = rimPaint.color..style = PaintingStyle.stroke..strokeWidth = 0.8);

      // Rear Dual Drive Tires
      for (final rOff in [5.5, 12.0]) {
        final rTire = RRect.fromRectAndRadius(
          Rect.fromCenter(center: origin.translate(side * 12.0, rOff), width: 5.2, height: 8.5),
          const Radius.circular(1.5),
        );
        canvas.drawRRect(rTire, tirePaint);
        canvas.drawRRect(rTire, Paint()..color = rimPaint.color..style = PaintingStyle.stroke..strokeWidth = 0.8);
      }
    }

    // 5. Heavy Dump Body (Hauler Tray / Bed)
    final dumpBed = RRect.fromRectAndRadius(
      Rect.fromCenter(center: origin.translate(0, 7.5), width: 20, height: 21),
      const Radius.circular(2.5),
    );
    canvas.drawRRect(dumpBed, Paint()..color = const Color(0xFF04121F));
    canvas.drawRRect(
      dumpBed,
      Paint()
        ..color = truckColor
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.3,
    );

    // Dump Bed Ribs (Reinforced Steel Ribs)
    for (final ribY in [1.0, 6.5, 12.0]) {
      canvas.drawLine(
        Offset(origin.dx - 8.5, origin.dy + ribY),
        Offset(origin.dx + 8.5, origin.dy + ribY),
        Paint()
          ..color = truckColor.withValues(alpha: 0.40)
          ..strokeWidth = 0.9,
      );
    }

    // 6. Forward Cab & Canopy Structure
    final cabCanopy = RRect.fromRectAndRadius(
      Rect.fromCenter(center: origin.translate(0, -7.5), width: 18, height: 11),
      const Radius.circular(2.5),
    );
    canvas.drawRRect(cabCanopy, Paint()..color = const Color(0xFF0091EA));
    canvas.drawRRect(
      cabCanopy,
      Paint()
        ..color = Colors.white
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.2,
    );

    // Driver Cab Glass Visor
    final cabGlass = RRect.fromRectAndRadius(
      Rect.fromCenter(center: origin.translate(-2.5, -8.0), width: 8.5, height: 5.5),
      const Radius.circular(1.5),
    );
    canvas.drawRRect(cabGlass, Paint()..color = const Color(0xFF031018));
    canvas.drawRRect(
      cabGlass,
      Paint()
        ..color = const Color(0xFF00E5FF)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.8,
    );

    // 7. Rooftop Amber / Cyan Rotating Warning Strobe Beacon
    final strobeFlash = (math.sin(animPhase * math.pi * 4).abs() > 0.4);
    final strobeColor = strobeFlash ? const Color(0xFFFFCC00) : const Color(0xFFFF6600);
    canvas.drawCircle(
      origin.translate(0, -12),
      2.5,
      Paint()..color = strobeColor,
    );
    if (strobeFlash) {
      canvas.drawCircle(
        origin.translate(0, -12),
        6.0,
        Paint()..color = strobeColor.withValues(alpha: 0.45),
      );
    }

    // 8. Vehicle Badge Emblem (Cockpit Tag)
    final badgeText = TextPainter(
      text: TextSpan(
        text: '🚚 DT / HD',
        style: TextStyle(
          color: truckColor,
          fontSize: 7.5,
          fontWeight: FontWeight.w900,
          letterSpacing: 0.3,
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();

    final badgeRect = Rect.fromCenter(
      center: origin.translate(0, 24),
      width: badgeText.width + 8,
      height: 12,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(badgeRect, const Radius.circular(3)),
      Paint()..color = const Color(0xEE030E18),
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(badgeRect, const Radius.circular(3)),
      Paint()
        ..color = truckColor.withValues(alpha: 0.65)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.8,
    );
    badgeText.paint(canvas, Offset(badgeRect.left + 4, badgeRect.top + 1.5));
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
      oldDelegate.animPhase != animPhase ||
      oldDelegate.isDanger != isDanger;
}

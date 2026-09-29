import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../models/cabin_bearing.dart';
import '../models/fleet_models.dart';
import '../services/fms_api_service.dart';

class GpsTrackPainter extends CustomPainter {
  final List<CabinGpsPoint> track;
  final List<NearbyVehicle> nearbyVehicles;
  final List<RoadSegment> roadSegments;
  final double easting, northing, heading, targetEasting, targetNorthing;
  final String targetName;
  final bool held;

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
  });

  @override
  void paint(Canvas canvas, Size size) {
    final bounds = Offset.zero & size;
    canvas.save();
    canvas.clipRect(bounds);
    canvas.drawColor(const Color(0xFF071B22), BlendMode.src);

    final origin = Offset(size.width * 0.5, size.height * 0.63);
    final span = math.max(210.0, math.min(700.0, size.height * 1.1));
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

    final grid = Paint()
      ..color = const Color(0xFF21404A)
      ..strokeWidth = 0.7;
    final gridStep = 50 * pixelsPerMeter;
    for (double x = origin.dx % gridStep; x < size.width; x += gridStep) {
      canvas.drawLine(Offset(x, 0), Offset(x, size.height), grid);
    }
    for (double y = origin.dy % gridStep; y < size.height; y += gridStep) {
      canvas.drawLine(Offset(0, y), Offset(size.width, y), grid);
    }

    // --- 1. RENDER HAUL ROAD NETWORK (Nearby Haul Roads) ---
    if (roadSegments.isNotEmpty) {
      const maxRoadDist = 750.0;
      final nearbyRoads = roadSegments.where((seg) {
        final dStartE = (seg.startEasting - easting).abs();
        final dStartN = (seg.startNorthing - northing).abs();
        final dEndE = (seg.endEasting - easting).abs();
        final dEndN = (seg.endNorthing - northing).abs();
        return (dStartE < maxRoadDist && dStartN < maxRoadDist) ||
            (dEndE < maxRoadDist && dEndN < maxRoadDist);
      }).toList();

      for (final seg in nearbyRoads) {
        final p1 = project(seg.startEasting, seg.startNorthing);
        final p2 = project(seg.endEasting, seg.endNorthing);

        // Road width in screen pixels based on real mining road width
        final roadPixelWidth = (seg.laneWidth * pixelsPerMeter).clamp(10.0, 36.0);

        // A. Outer glowing shoulder
        canvas.drawLine(
          p1,
          p2,
          Paint()
            ..color = const Color(0xFF0F3E4C).withValues(alpha: 0.55)
            ..strokeWidth = roadPixelWidth + 5.0
            ..strokeCap = StrokeCap.round,
        );

        // B. Dark Asphalt / Haul Road Surface
        canvas.drawLine(
          p1,
          p2,
          Paint()
            ..color = const Color(0xFF0D252E)
            ..strokeWidth = roadPixelWidth
            ..strokeCap = StrokeCap.round,
        );

        // C. Road Edge Guideline
        canvas.drawLine(
          p1,
          p2,
          Paint()
            ..color = const Color(0xFF1E5B6E).withValues(alpha: 0.70)
            ..strokeWidth = roadPixelWidth
            ..strokeCap = StrokeCap.round
            ..style = PaintingStyle.stroke,
        );

        // D. Dashed Center Guideline (Cyber Cyan / Emerald)
        final roadVec = p2 - p1;
        final roadDist = roadVec.distance;
        if (roadDist > 6.0) {
          final dir = roadVec / roadDist;
          final dashPaint = Paint()
            ..color = const Color(0xFF00E5FF).withValues(alpha: 0.45)
            ..strokeWidth = 1.6
            ..strokeCap = StrokeCap.round;
          const dashLen = 7.0;
          const gapLen = 5.0;
          for (double d = 0; d < roadDist; d += (dashLen + gapLen)) {
            final startPt = p1 + dir * d;
            final endPt = p1 + dir * math.min(d + dashLen, roadDist);
            canvas.drawLine(startPt, endPt, dashPaint);
          }
        }

        // E. Junction Node Points
        final nodePaint = Paint()
          ..color = const Color(0xFF00E5FF).withValues(alpha: 0.35);
        canvas.drawCircle(p1, 2.2, nodePaint);
        canvas.drawCircle(p2, 2.2, nodePaint);
      }
    }

    // --- 2. RENDER GPS TRACK / BREADCRUMBS ---
    final pathPaint = Paint()
      ..color = held ? const Color(0xFFB89254) : const Color(0xFF40CBBB)
      ..strokeWidth = 3.5
      ..strokeCap = StrokeCap.round;
    for (int i = 1; i < track.length; i++) {
      final before = track[i - 1], after = track[i];
      final dx = after.easting - before.easting;
      final dy = after.northing - before.northing;
      final gapSeconds = after.recordedAt.difference(before.recordedAt).inSeconds;
      final maxPlausibleMeters = math.max(25, gapSeconds * 18);
      if (gapSeconds <= 0 ||
          gapSeconds > 60 ||
          dx * dx + dy * dy > maxPlausibleMeters * maxPlausibleMeters) {
        continue;
      }
      canvas.drawLine(
        project(before.easting, before.northing),
        project(after.easting, after.northing),
        pathPaint,
      );
    }
    for (final point in track) {
      canvas.drawCircle(
        project(point.easting, point.northing),
        2.4,
        Paint()..color = const Color(0xFF80E9DF),
      );
    }

    // --- 3. TARGET GUIDELINE & BADGE ---
    final target = project(targetEasting, targetNorthing);
    final safe = Rect.fromLTRB(22, 22, size.width - 22, size.height - 22);
    final dx = target.dx - origin.dx, dy = target.dy - origin.dy;
    double fraction = 1;
    if (dx > 0) fraction = math.min(fraction, (safe.right - origin.dx) / dx);
    if (dx < 0) fraction = math.min(fraction, (safe.left - origin.dx) / dx);
    if (dy > 0) fraction = math.min(fraction, (safe.bottom - origin.dy) / dy);
    if (dy < 0) fraction = math.min(fraction, (safe.top - origin.dy) / dy);
    fraction = fraction.clamp(0.0, 1.0);
    final marker = Offset(origin.dx + dx * fraction, origin.dy + dy * fraction);
    final guidePaint = Paint()
      ..color = held ? const Color(0xFFD7A85C) : const Color(0xFF13C9EC)
      ..strokeWidth = 2.2
      ..strokeCap = StrokeCap.round;
    final guideLength = (marker - origin).distance;
    if (guideLength > 0) {
      final direction = (marker - origin) / guideLength;
      for (double offset = 0; offset < guideLength; offset += 16) {
        canvas.drawLine(
          origin + direction * offset,
          origin + direction * math.min(offset + 8, guideLength),
          guidePaint,
        );
      }
    }
    final targetDistance = math.sqrt(
      math.pow(targetEasting - easting, 2) +
          math.pow(targetNorthing - northing, 2),
    );
    final distanceLabel = targetDistance >= 1000
        ? '${(targetDistance / 1000).toStringAsFixed(2)} km'
        : '${targetDistance.round()} m';
    final title = TextPainter(
      text: TextSpan(
        text: targetName,
        style: const TextStyle(
          color: Color(0xFFF1FAFA),
          fontSize: 11,
          fontWeight: FontWeight.w700,
        ),
      ),
      textDirection: TextDirection.ltr,
      maxLines: 1,
      ellipsis: '...',
    )..layout(maxWidth: math.max(48, math.min(130, size.width - 90)));
    final subtitle = TextPainter(
      text: TextSpan(
        text: '${fraction < 1 ? 'ARAH TUJUAN' : 'TITIK TUJUAN'}  $distanceLabel',
        style: TextStyle(color: guidePaint.color, fontSize: 9),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    final badgeWidth = math.max(title.width, subtitle.width) + 16;
    const badgeHeight = 39.0;
    final badgeX = (marker.dx + 16 + badgeWidth <= size.width - 8)
        ? marker.dx + 16
        : marker.dx - 16 - badgeWidth;
    final badgeY = (marker.dy - badgeHeight / 2).clamp(
      4.0,
      math.max(4.0, size.height - badgeHeight - 4),
    ).toDouble();
    final badge = Rect.fromLTWH(badgeX, badgeY, badgeWidth, badgeHeight);
    canvas.drawRRect(
      RRect.fromRectAndRadius(badge, const Radius.circular(4)),
      Paint()..color = const Color(0xF2082028),
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(badge, const Radius.circular(4)),
      Paint()
        ..color = guidePaint.color
        ..strokeWidth = 1
        ..style = PaintingStyle.stroke,
    );
    title.paint(canvas, Offset(badge.left + 8, badge.top + 4));
    subtitle.paint(canvas, Offset(badge.left + 8, badge.top + 21));
    canvas.drawCircle(
      marker,
      11,
      Paint()..color = guidePaint.color.withValues(alpha: 0.20),
    );
    canvas.drawCircle(marker, 7, Paint()..color = const Color(0xFF071B22));
    canvas.drawCircle(
      marker,
      7,
      Paint()
        ..color = guidePaint.color
        ..strokeWidth = 2
        ..style = PaintingStyle.stroke,
    );
    canvas.drawCircle(marker, 2.5, Paint()..color = guidePaint.color);

    // --- 4. NEARBY VEHICLES RADAR MARKERS ---
    if (!held) {
      final labels = <Rect>[];
      final visible = Rect.fromLTRB(16, 36, size.width - 16, size.height - 36);
      for (final unit in nearbyVehicles) {
        final point = origin + Offset(
          unit.lateralOffsetMeters * pixelsPerMeter,
          -unit.forwardOffsetMeters * pixelsPerMeter,
        );
        if (!visible.contains(point) || (point - origin).distance < 18) {
          continue;
        }

        final color = unit.unitType == 'EX'
            ? const Color(0xFFFFA35C)
            : unit.unitType == 'HD'
            ? const Color(0xFF6FBEF4)
            : const Color(0xFFD9C87A);
        canvas.drawCircle(point, 11, Paint()..color = color.withValues(alpha: 0.16));
        canvas.drawCircle(point, 4.5, Paint()..color = color);
        canvas.drawCircle(
          point,
          5.5,
          Paint()
            ..color = const Color(0xFF071B22)
            ..strokeWidth = 1
            ..style = PaintingStyle.stroke,
        );

        final label = TextPainter(
          text: TextSpan(
            text: '   m',
            style: const TextStyle(
              color: Color(0xFFE5F2F3),
              fontSize: 10,
              fontWeight: FontWeight.w600,
            ),
          ),
          textDirection: TextDirection.ltr,
          maxLines: 1,
          ellipsis: '...',
        )..layout(maxWidth: math.max(40, math.min(110, size.width - 48)));
        final labelWidth = label.width + 12;
        final labelHeight = label.height + 8;
        final x = point.dx + 11 + labelWidth <= size.width - 8
            ? point.dx + 11
            : point.dx - 11 - labelWidth;
        final y = (point.dy - labelHeight / 2).clamp(
          38.0,
          math.max(38.0, size.height - labelHeight - 28),
        ).toDouble();
        final rect = Rect.fromLTWH(x, y, labelWidth, labelHeight);
        if (x < 8 || labels.any((existing) => existing.overlaps(rect))) {
          continue;
        }
        labels.add(rect);
        canvas.drawRRect(
          RRect.fromRectAndRadius(rect, const Radius.circular(4)),
          Paint()..color = const Color(0xF20D2931),
        );
        canvas.drawRRect(
          RRect.fromRectAndRadius(rect, const Radius.circular(4)),
          Paint()
            ..color = color.withValues(alpha: 0.72)
            ..strokeWidth = 1
            ..style = PaintingStyle.stroke,
        );
        label.paint(canvas, Offset(x + 6, y + 4));
      }
    }

    // --- 5. CURRENT VEHICLE AVATAR ---
    final unitColor = held ? const Color(0xFFD7A85C) : const Color(0xFF45E1B3);
    canvas.drawCircle(
      origin,
      18,
      Paint()..color = unitColor.withValues(alpha: 0.13),
    );
    final vehicleBody = RRect.fromRectAndRadius(
      Rect.fromCenter(center: origin, width: 16, height: 25),
      const Radius.circular(3),
    );
    canvas.drawRRect(vehicleBody, Paint()..color = unitColor);
    canvas.drawRRect(
      vehicleBody,
      Paint()
        ..color = const Color(0xFFE6FFFA)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.2,
    );
    canvas.drawRect(
      Rect.fromCenter(center: origin.translate(0, -5), width: 11, height: 5),
      Paint()..color = const Color(0xFF0B3942),
    );
    final wheelPaint = Paint()..color = const Color(0xFF173E43);
    for (final side in [-1.0, 1.0]) {
      for (final offset in [-6.0, 6.0]) {
        canvas.drawRRect(
          RRect.fromRectAndRadius(
            Rect.fromCenter(
              center: origin.translate(side * 9, offset),
              width: 4,
              height: 6,
            ),
            const Radius.circular(1),
          ),
          wheelPaint,
        );
      }
    }

    // --- 6. SCALE BAR (50m) ---
    final bar = Paint()
      ..color = const Color(0xFF9DC1C5)
      ..strokeWidth = 2;
    final fiftyMeters = 50 * pixelsPerMeter;
    canvas.drawLine(
      Offset(18, size.height - 18),
      Offset(18 + fiftyMeters, size.height - 18),
      bar,
    );
    canvas.drawLine(
      Offset(18, size.height - 22),
      Offset(18, size.height - 14),
      bar,
    );
    canvas.drawLine(
      Offset(18 + fiftyMeters, size.height - 22),
      Offset(18 + fiftyMeters, size.height - 14),
      bar,
    );
    final label = TextPainter(
      text: const TextSpan(
        text: '50 m',
        style: TextStyle(color: Color(0xFFB5CFD2), fontSize: 10),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    label.paint(canvas, Offset(22 + fiftyMeters, size.height - 26));
    canvas.restore();
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
      oldDelegate.held != held;
}

import 'dart:math' as math;
import 'package:flutter/material.dart';
import '../theme/fms_theme.dart';

class VirexaFuturisticLogo extends StatefulWidget {
  final double size;
  final bool animate;
  final Color? primaryColor;
  final Color? secondaryColor;
  final bool showGlow;

  const VirexaFuturisticLogo({
    super.key,
    this.size = 120,
    this.animate = true,
    this.primaryColor,
    this.secondaryColor,
    this.showGlow = true,
  });

  @override
  State<VirexaFuturisticLogo> createState() => _VirexaFuturisticLogoState();
}

class _VirexaFuturisticLogoState extends State<VirexaFuturisticLogo>
    with SingleTickerProviderStateMixin {
  late AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 6),
    );
    if (widget.animate) {
      _controller.repeat();
    }
  }

  @override
  void didUpdateWidget(VirexaFuturisticLogo oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.animate && !_controller.isAnimating) {
      _controller.repeat();
    } else if (!widget.animate && _controller.isAnimating) {
      _controller.stop();
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final primary = widget.primaryColor ?? FmsTheme.cyanAccent;
    final secondary = widget.secondaryColor ?? FmsTheme.emeraldGreen;

    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        return CustomPaint(
          size: Size(widget.size, widget.size),
          painter: _FuturisticLogoPainter(
            animationValue: _controller.value,
            primaryColor: primary,
            secondaryColor: secondary,
            showGlow: widget.showGlow,
          ),
        );
      },
    );
  }
}

class _FuturisticLogoPainter extends CustomPainter {
  final double animationValue;
  final Color primaryColor;
  final Color secondaryColor;
  final bool showGlow;

  _FuturisticLogoPainter({
    required this.animationValue,
    required this.primaryColor,
    required this.secondaryColor,
    required this.showGlow,
  });

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final radius = size.width / 2;

    // 1. Ambient Background Glow
    if (showGlow) {
      final glowPaint = Paint()
        ..color = primaryColor.withValues(alpha: 0.16 + 0.10 * math.sin(animationValue * 2 * math.pi).abs())
        ..maskFilter = MaskFilter.blur(BlurStyle.normal, radius * 0.45);
      canvas.drawCircle(center, radius * 0.75, glowPaint);
    }

    // 2. Outer Rotating Orbital Ring (Counter Clockwise)
    final outerRingPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = size.width * 0.016
      ..color = primaryColor.withValues(alpha: 0.45);

    final angleOuter = -animationValue * 2 * math.pi;
    for (int i = 0; i < 4; i++) {
      final startAngle = angleOuter + (i * math.pi / 2) + 0.15;
      const sweepAngle = math.pi / 2 - 0.3;
      canvas.drawArc(
        Rect.fromCircle(center: center, radius: radius * 0.94),
        startAngle,
        sweepAngle,
        false,
        outerRingPaint,
      );
    }

    // Outer Orbital Tick Markers
    final tickPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = size.width * 0.024
      ..strokeCap = StrokeCap.round
      ..color = secondaryColor.withValues(alpha: 0.9);
    for (int i = 0; i < 8; i++) {
      final tickAngle = angleOuter + (i * math.pi / 4);
      final r1 = radius * 0.89;
      final r2 = radius * 0.97;
      canvas.drawLine(
        Offset(center.dx + r1 * math.cos(tickAngle), center.dy + r1 * math.sin(tickAngle)),
        Offset(center.dx + r2 * math.cos(tickAngle), center.dy + r2 * math.sin(tickAngle)),
        tickPaint,
      );
    }

    // 3. Inner Hexagonal Cyber Frame (Clockwise slow)
    final hexRadius = radius * 0.76;
    final hexAngle = animationValue * 2 * math.pi * 0.5;
    final hexPath = Path();
    for (int i = 0; i < 6; i++) {
      final a = hexAngle + (i * math.pi / 3);
      final pt = Offset(center.dx + hexRadius * math.cos(a), center.dy + hexRadius * math.sin(a));
      if (i == 0) {
        hexPath.moveTo(pt.dx, pt.dy);
      } else {
        hexPath.lineTo(pt.dx, pt.dy);
      }
    }
    hexPath.close();

    // Draw Hexagon Frame fill & stroke
    final hexFill = Paint()
      ..shader = RadialGradient(
        colors: [
          const Color(0xFF0F263D).withValues(alpha: 0.90),
          const Color(0xFF030E1A).withValues(alpha: 0.98),
        ],
      ).createShader(Rect.fromCircle(center: center, radius: hexRadius))
      ..style = PaintingStyle.fill;
    canvas.drawPath(hexPath, hexFill);

    final hexStroke = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = size.width * 0.024
      ..shader = SweepGradient(
        colors: [
          primaryColor,
          secondaryColor,
          const Color(0xFF00E5FF),
          primaryColor,
        ],
        transform: GradientRotation(hexAngle),
      ).createShader(Rect.fromCircle(center: center, radius: hexRadius));
    canvas.drawPath(hexPath, hexStroke);

    // 4. Futuristic Monogram "V" (Mining Tech Chevron Core)
    final vWidth = size.width * 0.44;
    final vHeight = size.height * 0.48;
    final vTop = center.dy - vHeight * 0.48;
    final vBottom = center.dy + vHeight * 0.48;

    // Left Wing of V
    final leftWing = Path()
      ..moveTo(center.dx - vWidth * 0.54, vTop)
      ..lineTo(center.dx - vWidth * 0.16, vTop)
      ..lineTo(center.dx - vWidth * 0.02, vBottom - vHeight * 0.22)
      ..lineTo(center.dx - vWidth * 0.28, vBottom - vHeight * 0.22)
      ..close();

    // Right Wing of V
    final rightWing = Path()
      ..moveTo(center.dx + vWidth * 0.54, vTop)
      ..lineTo(center.dx + vWidth * 0.16, vTop)
      ..lineTo(center.dx + vWidth * 0.02, vBottom - vHeight * 0.22)
      ..lineTo(center.dx + vWidth * 0.28, vBottom - vHeight * 0.22)
      ..close();

    // Apex Diamond Core of V
    final diamondCore = Path()
      ..moveTo(center.dx, vBottom)
      ..lineTo(center.dx - vWidth * 0.22, vBottom - vHeight * 0.28)
      ..lineTo(center.dx, vBottom - vHeight * 0.52)
      ..lineTo(center.dx + vWidth * 0.22, vBottom - vHeight * 0.28)
      ..close();

    // Draw Left Wing (Cyan Neon Shader)
    final leftPaint = Paint()
      ..shader = LinearGradient(
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
        colors: [
          primaryColor,
          const Color(0xFF00B4D8),
        ],
      ).createShader(Rect.fromLTWH(center.dx - vWidth, vTop, vWidth, vHeight))
      ..style = PaintingStyle.fill;

    // Draw Right Wing (Emerald Tech Shader)
    final rightPaint = Paint()
      ..shader = LinearGradient(
        begin: Alignment.topRight,
        end: Alignment.bottomLeft,
        colors: [
          secondaryColor,
          const Color(0xFF05D584),
        ],
      ).createShader(Rect.fromLTWH(center.dx, vTop, vWidth, vHeight))
      ..style = PaintingStyle.fill;

    // Draw Apex Diamond (Luminous White-Cyan Core)
    final apexPaint = Paint()
      ..shader = LinearGradient(
        begin: Alignment.topCenter,
        end: Alignment.bottomCenter,
        colors: [
          const Color(0xFFFFFFFF),
          primaryColor,
          secondaryColor,
        ],
      ).createShader(Rect.fromLTWH(center.dx - vWidth * 0.3, vBottom - vHeight * 0.52, vWidth * 0.6, vHeight * 0.52))
      ..style = PaintingStyle.fill;

    canvas.drawPath(leftWing, leftPaint);
    canvas.drawPath(rightWing, rightPaint);
    canvas.drawPath(diamondCore, apexPaint);

    // Glowing Outlines for V Logo
    final vGlowStroke = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = size.width * 0.015
      ..color = Colors.white.withValues(alpha: 0.8);
    canvas.drawPath(diamondCore, vGlowStroke);

    // 5. Sweeping Laser Radar Line
    final sweepLaserAngle = animationValue * 2 * math.pi;
    final laserLength = radius * 0.65;
    final laserTarget = Offset(
      center.dx + laserLength * math.cos(sweepLaserAngle),
      center.dy + laserLength * math.sin(sweepLaserAngle),
    );
    final laserPaint = Paint()
      ..shader = LinearGradient(
        colors: [
          Colors.transparent,
          primaryColor.withValues(alpha: 0.8),
          Colors.white,
        ],
      ).createShader(Rect.fromPoints(center, laserTarget))
      ..strokeWidth = size.width * 0.02
      ..strokeCap = StrokeCap.round;
    canvas.drawLine(center, laserTarget, laserPaint);

    // Central Telemetry Dot
    final centerDotPaint = Paint()
      ..color = Colors.white
      ..style = PaintingStyle.fill;
    canvas.drawCircle(center, size.width * 0.03, centerDotPaint);
  }

  @override
  bool shouldRepaint(covariant _FuturisticLogoPainter oldDelegate) {
    return oldDelegate.animationValue != animationValue ||
        oldDelegate.primaryColor != primaryColor ||
        oldDelegate.secondaryColor != secondaryColor ||
        oldDelegate.showGlow != showGlow;
  }
}

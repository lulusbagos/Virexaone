import 'dart:async';

import 'package:flutter/material.dart';

import '../services/fms_api_service.dart';
import '../theme/fms_theme.dart';
import '../widgets/virexa_futuristic_logo.dart';
import 'cabin_dashboard_screen.dart';
import 'login_screen.dart';

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen>
    with SingleTickerProviderStateMixin {
  late AnimationController _animController;
  final FmsApiService _api = FmsApiService();
  
  String _loadingStatus = '>> [INIT] QUANTUM TELEMETRY GATEWAY...';
  String _subStatus = 'CALIBRATING SPATIAL POSITIONING SENSORS';
  double _progress = 0.12;
  int _pingMs = 12;

  @override
  void initState() {
    super.initState();
    _animController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 5),
    )..repeat();

    _startBootSequence();
  }

  Future<void> _startBootSequence() async {
    await Future.delayed(const Duration(milliseconds: 400));
    if (!mounted) return;
    setState(() {
      _loadingStatus = '>> [FLEET] ESTABLISHING REALTIME MESH COMMS...';
      _subStatus = 'CONNECTING TO DISPATCH CONTROL ROOM';
      _progress = 0.38;
      _pingMs = 8;
    });

    try {
      await _api.syncFromBackend();
    } catch (_) {}

    await Future.delayed(const Duration(milliseconds: 550));
    if (!mounted) return;
    setState(() {
      _loadingStatus = '>> [GIS-MAP] TOPOGRAPHIC ROAD BUFFER LOADED';
      _subStatus = 'HAUL ROAD NODES & EXCAVATOR PITS SYNCED';
      _progress = 0.72;
      _pingMs = 14;
    });

    await Future.delayed(const Duration(milliseconds: 550));
    if (!mounted) return;
    setState(() {
      _loadingStatus = '>> [COCKPIT] 3D HUD & GPS HEADING MATRIX ONLINE';
      _subStatus = 'AUTONOMOUS SPATIAL GUIDANCE READY';
      _progress = 0.94;
    });

    await Future.delayed(const Duration(milliseconds: 500));
    if (!mounted) return;
    setState(() {
      _loadingStatus = '>> [STATUS] ALL SUBSYSTEMS OPERATIONAL';
      _subStatus = 'WELCOME TO VIREXA ONE FLEET SYSTEM';
      _progress = 1.0;
    });

    await Future.delayed(const Duration(milliseconds: 400));
    if (!mounted) return;

    Navigator.of(context).pushReplacement(
      PageRouteBuilder(
        pageBuilder: (context, anim, secAnim) => _api.isLoggedIn
            ? const CabinDashboardScreen()
            : LoginScreen(
                onLoginSuccess: () {
                  Navigator.of(context).pushReplacement(
                    MaterialPageRoute(
                      builder: (_) => const CabinDashboardScreen(),
                    ),
                  );
                },
              ),
        transitionsBuilder: (context, anim, secAnim, child) =>
            FadeTransition(opacity: anim, child: child),
        transitionDuration: const Duration(milliseconds: 500),
      ),
    );
  }

  @override
  void dispose() {
    _animController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF050B14),
      body: Stack(
        children: [
          // 1. Ambient Background Sci-Fi Grid
          Positioned.fill(
            child: AnimatedBuilder(
              animation: _animController,
              builder: (context, _) => CustomPaint(
                painter: _CyberGridPainter(
                  animationValue: _animController.value,
                ),
              ),
            ),
          ),

          // 2. Corner Bracket Tech Accents
          Positioned(top: 24, left: 24, child: _techCorner(0)),
          Positioned(top: 24, right: 24, child: _techCorner(1)),
          Positioned(bottom: 24, left: 24, child: _techCorner(2)),
          Positioned(bottom: 24, right: 24, child: _techCorner(3)),

          // 3. Center Content
          SafeArea(
            child: Center(
              child: SingleChildScrollView(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 480),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 28, vertical: 20),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        // Top Telemetry Tag
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
                          decoration: BoxDecoration(
                            color: const Color(0xFF0C1F33),
                            borderRadius: BorderRadius.circular(4),
                            border: Border.all(
                              color: FmsTheme.cyanAccent.withValues(alpha: 0.4),
                              width: 1,
                            ),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Container(
                                width: 7,
                                height: 7,
                                decoration: const BoxDecoration(
                                  color: FmsTheme.emeraldGreen,
                                  shape: BoxShape.circle,
                                ),
                              ),
                              const SizedBox(width: 8),
                              Text(
                                'SYSTEM BOOT // PROTOCOL 2.0.4',
                                style: TextStyle(
                                  color: FmsTheme.cyanAccent.withValues(alpha: 0.9),
                                  fontSize: 10,
                                  fontFamily: 'monospace',
                                  fontWeight: FontWeight.bold,
                                  letterSpacing: 1.4,
                                ),
                              ),
                              const SizedBox(width: 8),
                              Text(
                                '${_pingMs}ms',
                                style: const TextStyle(
                                  color: FmsTheme.emeraldGreen,
                                  fontSize: 10,
                                  fontFamily: 'monospace',
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ],
                          ),
                        ),

                        const SizedBox(height: 32),

                        // Futuristic Animated Logo
                        const VirexaFuturisticLogo(
                          size: 150,
                          animate: true,
                          showGlow: true,
                        ),

                        const SizedBox(height: 28),

                        // App Title
                        ShaderMask(
                          shaderCallback: (bounds) => const LinearGradient(
                            colors: [
                              Color(0xFFFFFFFF),
                              Color(0xFF00F2FE),
                              Color(0xFF00F5A0),
                            ],
                            stops: [0.0, 0.6, 1.0],
                          ).createShader(bounds),
                          child: const Text(
                            'VIREXA ONE',
                            style: TextStyle(
                              color: Colors.white,
                              fontSize: 32,
                              fontWeight: FontWeight.w900,
                              letterSpacing: 4.5,
                            ),
                          ),
                        ),

                        const SizedBox(height: 4),

                        Text(
                          'INTELLIGENT FLEET COCKPIT & DISPATCH',
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            color: FmsTheme.cyanAccent.withValues(alpha: 0.8),
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                            letterSpacing: 2.2,
                          ),
                        ),

                        const SizedBox(height: 36),

                        // Futuristic Progress Bar
                        Container(
                          width: double.infinity,
                          height: 6,
                          decoration: BoxDecoration(
                            color: const Color(0xFF091829),
                            borderRadius: BorderRadius.circular(3),
                            border: Border.all(
                              color: FmsTheme.cyanAccent.withValues(alpha: 0.25),
                            ),
                          ),
                          child: FractionallySizedBox(
                            alignment: Alignment.centerLeft,
                            widthFactor: _progress.clamp(0.0, 1.0),
                            child: Container(
                              decoration: BoxDecoration(
                                borderRadius: BorderRadius.circular(3),
                                gradient: const LinearGradient(
                                  colors: [
                                    Color(0xFF00B4D8),
                                    Color(0xFF00F2FE),
                                    Color(0xFF00F5A0),
                                  ],
                                ),
                                boxShadow: [
                                  BoxShadow(
                                    color: FmsTheme.cyanAccent.withValues(alpha: 0.6),
                                    blurRadius: 8,
                                    spreadRadius: 1,
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),

                        const SizedBox(height: 16),

                        // Telemetry Diagnostics Feed
                        Container(
                          width: double.infinity,
                          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                          decoration: BoxDecoration(
                            color: const Color(0xFF071424).withValues(alpha: 0.85),
                            borderRadius: BorderRadius.circular(6),
                            border: Border.all(
                              color: const Color(0xFF14324D),
                            ),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                _loadingStatus,
                                style: const TextStyle(
                                  color: Color(0xFFE2F1FF),
                                  fontSize: 11,
                                  fontFamily: 'monospace',
                                  fontWeight: FontWeight.bold,
                                  letterSpacing: 0.8,
                                ),
                              ),
                              const SizedBox(height: 3),
                              Text(
                                _subStatus,
                                style: TextStyle(
                                  color: FmsTheme.cyanAccent.withValues(alpha: 0.65),
                                  fontSize: 9.5,
                                  fontFamily: 'monospace',
                                  letterSpacing: 0.6,
                                ),
                              ),
                            ],
                          ),
                        ),

                        const SizedBox(height: 24),

                        // System Bottom Tag
                        Text(
                          'ASTHA MINING TELEMETRY SYSTEMS • BUILD 2026.09',
                          style: TextStyle(
                            color: FmsTheme.textMuted.withValues(alpha: 0.5),
                            fontSize: 9,
                            letterSpacing: 1.2,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _techCorner(int corner) {
    // 0: top-left, 1: top-right, 2: bottom-left, 3: bottom-right
    return CustomPaint(
      size: const Size(20, 20),
      painter: _CornerPainter(corner: corner),
    );
  }
}

class _CornerPainter extends CustomPainter {
  final int corner;
  _CornerPainter({required this.corner});

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = const Color(0xFF00F2FE).withValues(alpha: 0.6)
      ..strokeWidth = 2
      ..style = PaintingStyle.stroke;

    final path = Path();
    if (corner == 0) {
      // Top-Left
      path.moveTo(0, size.height);
      path.lineTo(0, 0);
      path.lineTo(size.width, 0);
    } else if (corner == 1) {
      // Top-Right
      path.moveTo(0, 0);
      path.lineTo(size.width, 0);
      path.lineTo(size.width, size.height);
    } else if (corner == 2) {
      // Bottom-Left
      path.moveTo(0, 0);
      path.lineTo(0, size.height);
      path.lineTo(size.width, size.height);
    } else {
      // Bottom-Right
      path.moveTo(0, size.height);
      path.lineTo(size.width, size.height);
      path.lineTo(size.width, 0);
    }
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

class _CyberGridPainter extends CustomPainter {
  final double animationValue;
  _CyberGridPainter({required this.animationValue});

  @override
  void paint(Canvas canvas, Size size) {
    final gridPaint = Paint()
      ..color = const Color(0xFF00F2FE).withValues(alpha: 0.04)
      ..strokeWidth = 1;

    const spacing = 36.0;
    for (double x = 0; x < size.width; x += spacing) {
      canvas.drawLine(Offset(x, 0), Offset(x, size.height), gridPaint);
    }
    for (double y = 0; y < size.height; y += spacing) {
      canvas.drawLine(Offset(0, y), Offset(size.width, y), gridPaint);
    }

    // Scanning Radar Sweep Light
    final scanY = (animationValue * size.height * 1.5) % (size.height + 100) - 50;
    final scanPaint = Paint()
      ..shader = LinearGradient(
        begin: Alignment.topCenter,
        end: Alignment.bottomCenter,
        colors: [
          Colors.transparent,
          const Color(0xFF00F2FE).withValues(alpha: 0.08),
          Colors.transparent,
        ],
      ).createShader(Rect.fromLTWH(0, scanY - 40, size.width, 80));

    canvas.drawRect(Rect.fromLTWH(0, scanY - 40, size.width, 80), scanPaint);
  }

  @override
  bool shouldRepaint(covariant _CyberGridPainter oldDelegate) =>
      oldDelegate.animationValue != animationValue;
}

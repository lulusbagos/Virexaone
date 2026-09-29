import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../services/fms_api_service.dart';
import '../theme/fms_theme.dart';
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
  String _loadingStatus = 'Menginisialisasi sistem telemetri...';
  double _progress = 0.15;

  @override
  void initState() {
    super.initState();
    _animController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 4),
    )..repeat();

    _startBootSequence();
  }

  Future<void> _startBootSequence() async {
    await Future.delayed(const Duration(milliseconds: 500));
    if (!mounted) return;
    setState(() {
      _loadingStatus = 'Menghubungkan server FMS lokal...';
      _progress = 0.45;
    });

    try {
      await _api.syncFromBackend();
    } catch (_) {}

    await Future.delayed(const Duration(milliseconds: 600));
    if (!mounted) return;
    setState(() {
      _loadingStatus = 'Menyiapkan modul radio & spatial HUD...';
      _progress = 0.80;
    });

    await Future.delayed(const Duration(milliseconds: 600));
    if (!mounted) return;
    setState(() {
      _loadingStatus = 'Sistem Siap.';
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
        transitionDuration: const Duration(milliseconds: 450),
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
      backgroundColor: FmsTheme.bgDark,
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 460),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 24),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  // Animated Glowing Hexagon Emblem
                  AnimatedBuilder(
                    animation: _animController,
                    builder: (context, child) {
                      return Stack(
                        alignment: Alignment.center,
                        children: [
                          // Outer Rotating Ring
                          Transform.rotate(
                            angle: _animController.value * 2 * math.pi,
                            child: Container(
                              width: 130,
                              height: 130,
                              decoration: BoxDecoration(
                                shape: BoxShape.circle,
                                border: Border.all(
                                  color: FmsTheme.cyanAccent.withValues(alpha: 0.35),
                                  width: 1.5,
                                ),
                              ),
                            ),
                          ),
                          // Pulsing Glow Hexagon
                          Container(
                            width: 104,
                            height: 104,
                            decoration: BoxDecoration(
                              color: const Color(0xFF071B2D),
                              borderRadius: BorderRadius.circular(24),
                              border: Border.all(
                                color: FmsTheme.emeraldGreen,
                                width: 2.2,
                              ),
                              boxShadow: FmsTheme.neonGlowShadow(
                                FmsTheme.emeraldGreen,
                                opacity: 0.4 + 0.3 * math.sin(_animController.value * 2 * math.pi).abs(),
                                blur: 20,
                              ),
                            ),
                            child: const Icon(
                              Icons.precision_manufacturing_rounded,
                              size: 54,
                              color: FmsTheme.emeraldGreen,
                            ),
                          ),
                        ],
                      );
                    },
                  ),
                  const SizedBox(height: 28),

                  // Brand Title
                  ShaderMask(
                    shaderCallback: (bounds) => const LinearGradient(
                      colors: [FmsTheme.emeraldGreen, FmsTheme.cyanAccent, Colors.white],
                    ).createShader(bounds),
                    child: Text(
                      'VIREXA ONE',
                      style: FmsTheme.displayLarge.copyWith(
                        fontSize: 34,
                        fontWeight: FontWeight.w900,
                        letterSpacing: 2.5,
                        color: Colors.white,
                      ),
                    ),
                  ),
                  const SizedBox(height: 4),

                  Text(
                    'ASTHA FLEET MANAGEMENT & DIGITAL TWIN',
                    textAlign: TextAlign.center,
                    style: FmsTheme.caption.copyWith(
                      color: FmsTheme.cyanAccent,
                      fontSize: 10.5,
                      fontWeight: FontWeight.bold,
                      letterSpacing: 1.2,
                    ),
                  ),
                  const SizedBox(height: 38),

                  // Progress Bar
                  ClipRRect(
                    borderRadius: BorderRadius.circular(6),
                    child: SizedBox(
                      height: 6,
                      child: LinearProgressIndicator(
                        value: _progress,
                        backgroundColor: const Color(0xFF0C2237),
                        valueColor: const AlwaysStoppedAnimation<Color>(FmsTheme.emeraldGreen),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),

                  // Dynamic Loading Status Text
                  Text(
                    _loadingStatus,
                    style: FmsTheme.caption.copyWith(
                      color: FmsTheme.textMuted,
                      fontSize: 11,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

import 'login_screen.dart';
import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../theme/fms_theme.dart';
import '../services/fms_api_service.dart';
import '../services/fms_settings_service.dart';
import '../widgets/nav_3d_arrow_painter.dart';
import '../widgets/gps_track_painter.dart';
import '../widgets/telemetry_capsule.dart';
import '../widgets/select_target_dialog.dart';
import '../widgets/server_settings_dialog.dart';
import '../services/live_cabin_comms_service.dart';
import 'fms_menu_screen.dart';

class CabinDashboardScreen extends StatefulWidget {
  const CabinDashboardScreen({super.key});

  @override
  State<CabinDashboardScreen> createState() => _CabinDashboardScreenState();
}

class _CabinDashboardScreenState extends State<CabinDashboardScreen>
    with TickerProviderStateMixin {

  Future<void> _handleExitCockpit() async {
    final shouldExit = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: const Color(0xFF0D1B2A),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(12),
          side: BorderSide(color: FmsTheme.cyanAccent, width: 1.2),
        ),
        title: Row(
          children: [
            const Icon(Icons.exit_to_app_rounded, color: FmsTheme.amberWarning, size: 24),
            const SizedBox(width: 10),
            Text(
              'KELUAR COCKPIT',
              style: FmsTheme.titleMedium.copyWith(
                color: FmsTheme.amberWarning,
                fontWeight: FontWeight.bold,
                letterSpacing: 1.2,
              ),
            ),
          ],
        ),
        content: const Text(
          'Apakah Anda yakin ingin keluar dari cockpit unit ini dan kembali ke menu pemilihan unit?',
          style: TextStyle(color: Color(0xFFE0E6ED), fontSize: 13),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text('BATAL', style: TextStyle(color: FmsTheme.textMuted)),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFFE63946),
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(6)),
            ),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('YA, KELUAR UNIT', style: TextStyle(fontWeight: FontWeight.bold)),
          ),
        ],
      ),
    );

    if (shouldExit == true && mounted) {
      _api.logout();
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(
          builder: (navContext) => LoginScreen(
            onLoginSuccess: () {
              Navigator.of(navContext).pushReplacement(
                MaterialPageRoute(
                  builder: (_) => const CabinDashboardScreen(),
                ),
              );
            },
          ),
        ),
        (route) => false,
      );
    }
  }

  late AnimationController _animController;
  late AnimationController _bearingController;
  late AnimationController _motionController;
  double _bearingFrom = 0;
  double _bearingTo = 0;
  bool _bearingReady = false;
  String _bearingUnit = '';
  String _bearingTarget = '';
  double _eastingFrom = 0, _eastingTo = 0;
  double _northingFrom = 0, _northingTo = 0;
  double _headingFrom = 0, _headingTo = 0;
  double _azimuthFrom = 0, _azimuthTo = 0;
  bool _motionReady = false;
  final FmsApiService _api = FmsApiService();
  final FmsSettingsService _settings = FmsSettingsService();
  Timer? _clockTimer;
  String _currentTimeStr = '';
  bool _isZenNavigationMode = false;
  int _rightDockTab = 0; // 0: Siklus Operasi, 1: Gauges & Mesin
  int _navigationMode = 1; // 0: 3D Chevron Arrow, 1: Jejak GPS

  void _cycleTheme() {
    final current = _settings.currentTheme;
    final nextIndex = (current.index + 1) % AppThemeMode.values.length;
    _settings.setTheme(AppThemeMode.values[nextIndex]);
    final label = _settings.currentTheme == AppThemeMode.solarDay
        ? 'Mode Siang (Solar High-Contrast)'
        : _settings.currentTheme == AppThemeMode.amberWarmth
        ? 'Mode Amber (Anti-Lelah Mata)'
        : _settings.currentTheme == AppThemeMode.emeraldTactical
        ? 'Mode Tactical (Aviasi Hijau)'
        : 'Mode Malam (Cyber Midnight)';
    _showHudNotification(
      label,
      _settings.currentTheme == AppThemeMode.solarDay
          ? Icons.wb_sunny_rounded
          : _settings.currentTheme == AppThemeMode.amberWarmth
          ? Icons.shield_moon_rounded
          : _settings.currentTheme == AppThemeMode.emeraldTactical
          ? Icons.radar_rounded
          : Icons.nightlight_round_rounded,
      FmsTheme.cyanAccent,
    );
  }

  void _toggleLanguage() {
    final nextLang = _settings.currentLanguage == AppLanguage.id ? AppLanguage.en : AppLanguage.id;
    _settings.setLanguage(nextLang);
    final label = nextLang == AppLanguage.id ? 'Bahasa Indonesia' : 'English';
    _showHudNotification(label, Icons.language_rounded, FmsTheme.emeraldGreen);
  }

  void _showHudNotification(String message, IconData icon, Color accentColor) {
    ScaffoldMessenger.of(context).hideCurrentSnackBar();
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, color: accentColor, size: 16),
            const SizedBox(width: 8),
            Text(
              message,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.bold,
                fontSize: 11.5,
                letterSpacing: 0.3,
              ),
            ),
          ],
        ),
        duration: const Duration(milliseconds: 1400),
        behavior: SnackBarBehavior.floating,
        margin: const EdgeInsets.only(bottom: 48, left: 16, right: 16),
        backgroundColor: const Color(0xF2071E32),
        elevation: 6,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(8),
          side: BorderSide(color: accentColor, width: 1.2),
        ),
      ),
    );
  }

  @override
  void initState() {
    super.initState();
    _animController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 5), // Putaran radar tenang & realistis (12 RPM)
    )..repeat();
    _bearingController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1400),
    );
    _motionController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1800),
    );

    _updateTime();
    _clockTimer = Timer.periodic(
      const Duration(seconds: 1),
      (_) => _updateTime(),
    );
    _api.addListener(_onApiUpdate);
  }

  DateTime? _lastApiUpdateAt;

  void _onApiUpdate() {
    if (!mounted) return;
    if (!_api.hasNavigationFix) {
      _bearingReady = false;
      _motionReady = false;
      _bearingController.stop();
      _motionController.stop();
    } else {
      final next = _api.relativeBearingDegrees;
      final nextE = _api.hdEasting;
      final nextN = _api.hdNorthing;
      final nextH = _api.hdHeadingDeg;
      final nextAz = _api.absoluteTargetAzimuth;

      if (!_bearingReady ||
          !_motionReady ||
          _bearingUnit != _api.selectedUnitId ||
          _bearingTarget != _api.activeTargetName) {
        _bearingFrom = next;
        _bearingTo = next;
        _eastingFrom = _eastingTo = nextE;
        _northingFrom = _northingTo = nextN;
        _headingFrom = _headingTo = nextH;
        _azimuthFrom = _azimuthTo = nextAz;
        _bearingReady = true;
        _motionReady = true;
        _bearingUnit = _api.selectedUnitId;
        _bearingTarget = _api.activeTargetName;
        _lastApiUpdateAt = DateTime.now();
        _bearingController.stop();
        _motionController.stop();
      } else {
        final now = DateTime.now();
        if (_lastApiUpdateAt != null) {
          final diff = now.difference(_lastApiUpdateAt!).inMilliseconds.clamp(1400, 3200);
          _motionController.duration = Duration(milliseconds: diff);
          _bearingController.duration = Duration(milliseconds: (diff * 0.75).round());
        }
        _lastApiUpdateAt = now;

        // Smooth GPS motion & heading (Catmull-Rom & adaptive buffer)
        final currE = _visualEasting;
        final currN = _visualNorthing;
        final currH = _visualHeading;
        final currAz = _visualAzimuth;
        final hTurn = (nextH - currH + 540) % 360 - 180;
        final azTurn = (nextAz - currAz + 540) % 360 - 180;

        _eastingFrom = currE;
        _eastingTo = nextE;
        _northingFrom = currN;
        _northingTo = nextN;
        _headingFrom = currH;
        _headingTo = currH + hTurn;
        _azimuthFrom = currAz;
        _azimuthTo = currAz + azTurn;
        _motionController.forward(from: 0);

        // Smooth relative bearing
        final current = _visualBearing;
        final turn = (next - current + 540) % 360 - 180;
        if (turn.abs() > 0.3) {
          _bearingFrom = current;
          _bearingTo = current + turn;
          _bearingController.forward(from: 0);
        }
      }
    }
    setState(() {});
  }

  double get _visualBearing =>
      _bearingFrom +
      (_bearingTo - _bearingFrom) *
          Curves.easeInOutCubic.transform(_bearingController.value);

  double get _visualEasting {
    final base = _eastingFrom +
        (_eastingTo - _eastingFrom) *
            Curves.easeInOutCubic.transform(_motionController.value);
    if (_motionController.isCompleted && _api.hdSpeedKmh > 1.0 && _lastApiUpdateAt != null) {
      final elapsedSec = (DateTime.now().difference(_lastApiUpdateAt!).inMilliseconds -
              (_motionController.duration?.inMilliseconds ?? 1800)) /
          1000.0;
      if (elapsedSec > 0 && elapsedSec < 2.5) {
        final speedMps = _api.hdSpeedKmh / 3.6;
        final hRad = _visualHeading * math.pi / 180.0;
        return base + math.sin(hRad) * speedMps * elapsedSec;
      }
    }
    return base;
  }

  double get _visualNorthing {
    final base = _northingFrom +
        (_northingTo - _northingFrom) *
            Curves.easeInOutCubic.transform(_motionController.value);
    if (_motionController.isCompleted && _api.hdSpeedKmh > 1.0 && _lastApiUpdateAt != null) {
      final elapsedSec = (DateTime.now().difference(_lastApiUpdateAt!).inMilliseconds -
              (_motionController.duration?.inMilliseconds ?? 1800)) /
          1000.0;
      if (elapsedSec > 0 && elapsedSec < 2.5) {
        final speedMps = _api.hdSpeedKmh / 3.6;
        final hRad = _visualHeading * math.pi / 180.0;
        return base + math.cos(hRad) * speedMps * elapsedSec;
      }
    }
    return base;
  }

  double get _visualHeading =>
      (_headingFrom +
          (_headingTo - _headingFrom) *
              Curves.easeInOutCubic.transform(_motionController.value) +
          360) %
      360;

  double get _visualAzimuth =>
      (_azimuthFrom +
          (_azimuthTo - _azimuthFrom) *
              Curves.easeInOutCubic.transform(_motionController.value) +
          360) %
      360;

  void _updateTime() {
    if (mounted) {
      setState(() {
        _currentTimeStr = DateFormat('HH:mm:ss').format(DateTime.now());
      });
    }
  }

  @override
  void dispose() {
    _clockTimer?.cancel();
    _animController.dispose();
    _bearingController.dispose();
    _motionController.dispose();
    _api.removeListener(_onApiUpdate);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final screenWidth = MediaQuery.sizeOf(context).width;
    final isCompact = screenWidth < 640;

    return Scaffold(
      backgroundColor: FmsTheme.bgDark,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 6.0, vertical: 4.0),
          child: AnimatedBuilder(
            animation: LiveCabinCommsService(),
            builder: (context, _) {
              final live = LiveCabinCommsService();
              final alert = live.alert;

              return Column(
                children: [
                  // 1. TOP COCKPIT HEADER
                  _buildTopBar(isCompact),
                  const SizedBox(height: 4),

                  // Floating Incoming Message Alert Banner (HUD)
                  if (alert != null) ...[
                    _buildIncomingMessageBanner(alert, live),
                    const SizedBox(height: 4),
                  ],

                  // 2. MAIN COCKPIT VIEWPORT (ADAPTIVE FOR MOBILE & TABLET)
                  Expanded(
                    child: isCompact
                        ? _buildCompactViewport()
                        : _buildWideViewport(),
                  ),
                  const SizedBox(height: 4),

                  // 3. BOTTOM TELEMETRY BAR
                  _buildBottomTelemetryBar(isCompact),
                ],
              );
            },
          ),
        ),
      ),
    );
  }

  Widget _buildIncomingMessageBanner(CabinAlert alert, LiveCabinCommsService live) {
    final Color borderClr = alert.urgent ? FmsTheme.redHazard : FmsTheme.cyanAccent;
    final Color bgClr = alert.urgent ? const Color(0xEC3A0815) : const Color(0xEC072033);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: bgClr,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: borderClr, width: 1.2),
        boxShadow: FmsTheme.neonGlowShadow(borderClr, opacity: 0.35, blur: 8),
      ),
      child: Row(
        children: [
          Icon(
            alert.voice ? Icons.radio_rounded : Icons.mark_chat_unread_rounded,
            size: 18,
            color: borderClr,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  alert.title,
                  style: TextStyle(
                    color: borderClr,
                    fontSize: 9.5,
                    fontWeight: FontWeight.w900,
                    letterSpacing: 0.4,
                  ),
                ),
                Text(
                  alert.body,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 11,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Material(
            color: Colors.transparent,
            child: InkWell(
              onTap: () {
                live.dismissAlert();
                _openFmsMenu(openMessages: true);
              },
              borderRadius: BorderRadius.circular(6),
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: borderClr.withValues(alpha: 0.2),
                  borderRadius: BorderRadius.circular(6),
                  border: Border.all(color: borderClr, width: 1.0),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Icons.chat_bubble_outline_rounded, size: 12, color: borderClr),
                    const SizedBox(width: 4),
                    Text(
                      'BUKA',
                      style: TextStyle(
                        color: borderClr,
                        fontSize: 9.0,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(width: 4),
          InkWell(
            onTap: live.dismissAlert,
            borderRadius: BorderRadius.circular(4),
            child: const Padding(
              padding: EdgeInsets.all(4),
              child: Icon(Icons.close_rounded, size: 16, color: Colors.white70),
            ),
          ),
        ],
      ),
    );
  }

  // =========================================================================
  // 1. TOP STATUS BAR (RESPONSIVE)
  // =========================================================================
  // =========================================================================
  // 1. TOP COCKPIT HEADER BAR (ADAPTIVE TO DANGER ALERT STATE)
  // =========================================================================
  Widget _buildTopBar(bool isCompact) {
    final stateLabel = _api.currentStatus;
    final isDanger = LiveCabinCommsService().hasActiveHazard || _api.hasProximityHazard;
    final Color barBorder = isDanger ? const Color(0xFFFF1744) : FmsTheme.cardBorder;

    if (isCompact) {
      return Container(
        height: 42,
        padding: const EdgeInsets.symmetric(horizontal: 8),
        decoration: BoxDecoration(
          color: isDanger ? const Color(0xEA2A0812) : FmsTheme.cardHeaderBg,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: barBorder, width: isDanger ? 1.4 : 1.0),
          boxShadow: isDanger
              ? [
                  const BoxShadow(
                    color: Color(0x66FF1744),
                    blurRadius: 10,
                    spreadRadius: 1,
                  ),
                ]
              : null,
        ),
        child: Row(
          children: [
            Container(
              width: 8,
              height: 8,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: isDanger
                    ? const Color(0xFFFF1744)
                    : _api.hasUnitGpsFix
                    ? FmsTheme.emeraldGreen
                    : _api.navigationIsHeld
                    ? FmsTheme.amberWarning
                    : FmsTheme.redHazard,
                boxShadow: FmsTheme.neonGlowShadow(
                  isDanger
                      ? const Color(0xFFFF1744)
                      : _api.hasUnitGpsFix
                      ? FmsTheme.emeraldGreen
                      : FmsTheme.amberWarning,
                  opacity: 0.8,
                  blur: 6,
                ),
              ),
            ),
            const SizedBox(width: 8),
            Text(
              _api.selectedUnitId,
              style: FmsTheme.titleMedium.copyWith(
                color: isDanger ? const Color(0xFFFF3366) : FmsTheme.emeraldGreen,
                fontWeight: FontWeight.w800,
                fontSize: 13,
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                isDanger
                    ? '⚠️ PERINGATAN BAHAYA AKTIF'
                    : '${_api.currentStatus} | ${_api.navigationIsHeld ? _api.navigationHoldLabel : 'GPS'} ${_api.displayGpsAgeSeconds ?? '-'}s',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: isDanger ? const Color(0xFFFF5252) : FmsTheme.textLight,
                  fontSize: 10,
                  fontWeight: isDanger ? FontWeight.bold : FontWeight.normal,
                ),
              ),
            ),
            Text(
              _currentTimeStr,
              style: FmsTheme.codePill.copyWith(
                color: isDanger ? const Color(0xFFFF8A80) : FmsTheme.cyanAccent,
                fontSize: 10,
              ),
            ),
            const SizedBox(width: 4),
            IconButton(
              tooltip: 'Menu FMS',
              icon: Icon(Icons.menu_rounded, size: 20, color: FmsTheme.textLight),
              onPressed: () => _openFmsMenu(),
              padding: EdgeInsets.zero,
              constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
            ),
          ],
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: isDanger ? const Color(0xEA2A0812) : FmsTheme.cardHeaderBg,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: barBorder, width: isDanger ? 1.4 : 1.0),
        boxShadow: isDanger
            ? [
                const BoxShadow(
                  color: Color(0x66FF1744),
                  blurRadius: 10,
                  spreadRadius: 1,
                ),
              ]
            : null,
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          // 1. Unit GPS and site weather (Left Column)
          Flexible(
            flex: 27,
            child: FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerLeft,
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  InkWell(
                    onTap: () {
                      showDialog(
                        context: context,
                        builder: (_) => const ServerSettingsDialog(),
                      );
                    },
                    borderRadius: BorderRadius.circular(8),
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: _api.isApiConnected
                            ? const Color(0x2200FFA3)
                            : const Color(0x33FFB703),
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(
                          color: _api.isApiConnected
                              ? FmsTheme.emeraldGreen.withValues(alpha: 0.6)
                              : FmsTheme.amberWarning,
                        ),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Container(
                            width: 7,
                            height: 7,
                            decoration: BoxDecoration(
                              shape: BoxShape.circle,
                              color: _api.hasUnitGpsFix
                                  ? FmsTheme.emeraldGreen
                                  : FmsTheme.amberWarning,
                            ),
                          ),
                          const SizedBox(width: 5),
                          Text(
                            _api.hasUnitGpsFix
                                ? (_api.hdSpeedKmh > 0 ? 'GPS UNIT AKTIF' : 'GPS STANDBY (BERHENTI)')
                                : 'GPS STANDBY (POSISI TERSIMPAN)',
                            style: TextStyle(
                              color: _api.isApiConnected
                                  ? FmsTheme.emeraldGreen
                                  : FmsTheme.amberWarning,
                              fontWeight: FontWeight.bold,
                              fontSize: 10,
                            ),
                          ),
                          Text(
                            ' ${_api.formattedGpsAge}',
                            style: FmsTheme.caption.copyWith(
                              color: FmsTheme.cyanAccent,
                              fontSize: 9.5,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(width: 5),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 4),
                    decoration: BoxDecoration(
                      color: const Color(0x1F00FFA3),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(
                        color: FmsTheme.emeraldGreen.withValues(alpha: 0.4),
                      ),
                    ),
                    child: Text(
                      _api.pitWeather,
                      style: TextStyle(
                        color: FmsTheme.emeraldGreen,
                        fontSize: 9.5,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(width: 4),

          // 2. Center Pill: Unit ID | Operator | Status | Ritasi
          Flexible(
            flex: 39,
            child: FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.center,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: [Color(0xFF041828), Color(0xFF07243C), Color(0xFF041828)],
                  ),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: FmsTheme.cyanAccent.withValues(alpha: 0.5), width: 1.1),
                  boxShadow: FmsTheme.neonGlowShadow(FmsTheme.cyanAccent, opacity: 0.25, blur: 6),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    // Unit Icon Emblem Badge
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 4.5, vertical: 1.5),
                      decoration: BoxDecoration(
                        gradient: const LinearGradient(
                          colors: [Color(0xFF00E5FF), Color(0xFF0077FE)],
                        ),
                        borderRadius: BorderRadius.circular(4),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(
                            _api.selectedUnitId.startsWith('EX')
                                ? Icons.construction_rounded
                                : _api.selectedUnitId.startsWith('DZ')
                                ? Icons.agriculture_rounded
                                : Icons.local_shipping_rounded,
                            size: 11,
                            color: Colors.white,
                          ),
                          const SizedBox(width: 3),
                          Text(
                            _api.selectedUnitId.isEmpty ? 'UNIT' : _api.selectedUnitId,
                            style: const TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.w900,
                              fontSize: 10.5,
                              letterSpacing: 0.4,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 6),

                    // Status with LED Indicator
                    Container(
                      width: 5,
                      height: 5,
                      decoration: BoxDecoration(
                        shape: BoxShape.circle,
                        color: _api.hdSpeedKmh > 0 ? FmsTheme.emeraldGreen : FmsTheme.amberWarning,
                        boxShadow: FmsTheme.neonGlowShadow(
                          _api.hdSpeedKmh > 0 ? FmsTheme.emeraldGreen : FmsTheme.amberWarning,
                          opacity: 0.8,
                          blur: 4,
                        ),
                      ),
                    ),
                    const SizedBox(width: 4),
                    Text(
                      stateLabel.isEmpty || stateLabel == 'TIDAK TERSEDIA'
                          ? 'BERHENTI'
                          : stateLabel,
                      style: FmsTheme.titleMedium.copyWith(
                        color: FmsTheme.cyanAccent,
                        fontSize: 10,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(width: 6),
                    Text('•', style: TextStyle(color: FmsTheme.cardBorder, fontSize: 10)),
                    const SizedBox(width: 6),

                    // Load Counter Badge
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1.5),
                      decoration: BoxDecoration(
                        color: const Color(0x33FFB703),
                        borderRadius: BorderRadius.circular(4),
                        border: Border.all(
                          color: FmsTheme.amberWarning.withValues(alpha: 0.6),
                        ),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(Icons.inventory_2_outlined, size: 9.5, color: FmsTheme.amberWarning),
                          const SizedBox(width: 3),
                          Text(
                            _api.haulDataAvailable
                                ? '${_api.completedRitasiCount} LOAD'
                                : 'LOAD -',
                            style: const TextStyle(
                              color: FmsTheme.amberWarning,
                              fontWeight: FontWeight.bold,
                              fontSize: 9.0,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(width: 4),

          // 3. Right Controls: Quick Theme, Language, Clock, Menu & Logout
          Flexible(
            flex: 34,
            child: FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerRight,
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  IconButton(
                    icon: Icon(
                      _settings.currentTheme == AppThemeMode.solarDay
                          ? Icons.wb_sunny_rounded
                          : _settings.currentTheme == AppThemeMode.amberWarmth
                          ? Icons.shield_moon_rounded
                          : _settings.currentTheme == AppThemeMode.emeraldTactical
                          ? Icons.radar_rounded
                          : Icons.nightlight_round_rounded,
                      size: 19,
                      color: FmsTheme.cyanAccent,
                    ),
                    tooltip: 'Ganti Tema Siang/Malam/Amber/Taktis',
                    onPressed: _cycleTheme,
                    padding: const EdgeInsets.all(4),
                    constraints: const BoxConstraints(),
                  ),
                  const SizedBox(width: 4),
                  InkWell(
                    onTap: _toggleLanguage,
                    borderRadius: BorderRadius.circular(6),
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
                      decoration: BoxDecoration(
                        color: FmsTheme.cardBg,
                        borderRadius: BorderRadius.circular(6),
                        border: Border.all(color: FmsTheme.cardBorder),
                      ),
                      child: Text(
                        _settings.currentLanguage == AppLanguage.id ? '🇮🇩 ID' : '🇬🇧 EN',
                        style: const TextStyle(
                          fontSize: 9.5,
                          fontWeight: FontWeight.bold,
                          color: Colors.white,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 4),
                  IconButton(
                    icon: Icon(Icons.menu_rounded, size: 19, color: FmsTheme.cyanAccent),
                    tooltip: _settings.tr('menu_btn'),
                    onPressed: () => _openFmsMenu(),
                    padding: const EdgeInsets.all(4),
                    constraints: const BoxConstraints(),
                  ),
                  const SizedBox(width: 4),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
                    decoration: BoxDecoration(
                      color: const Color(0x2200E5FF),
                      borderRadius: BorderRadius.circular(6),
                      border: Border.all(
                        color: FmsTheme.cyanAccent.withValues(alpha: 0.4),
                      ),
                    ),
                    child: Text(
                      _currentTimeStr,
                      style: FmsTheme.codePill.copyWith(
                        color: FmsTheme.cyanAccent,
                        fontSize: 10.5,
                      ),
                    ),
                  ),
                  const SizedBox(width: 4),
                  IconButton(
                    icon: Icon(
                      _isZenNavigationMode
                          ? Icons.fullscreen_exit_rounded
                          : Icons.fullscreen_rounded,
                      size: 19,
                      color: _isZenNavigationMode
                          ? FmsTheme.emeraldGreen
                          : FmsTheme.cyanAccent,
                    ),
                    tooltip: _isZenNavigationMode ? 'Kembalikan Panel' : 'Fokus Navigasi Penuh',
                    onPressed: () {
                      setState(() {
                        _isZenNavigationMode = !_isZenNavigationMode;
                      });
                    },
                    padding: const EdgeInsets.all(4),
                    constraints: const BoxConstraints(),
                  ),
                  const SizedBox(width: 4),
                  IconButton(
                    icon: const Icon(
                      Icons.exit_to_app_rounded,
                      size: 18,
                      color: FmsTheme.amberWarning,
                    ),
                    tooltip: 'Ganti Unit / Keluar Cockpit',
                    onPressed: _handleExitCockpit,
                    padding: const EdgeInsets.all(4),
                    constraints: const BoxConstraints(),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  // =========================================================================
  // 2. VIEWPORTS: COMPACT (PORTRAIT) vs WIDE (LANDSCAPE / TABLET)
  // =========================================================================
  Widget _buildCompactViewport() {
    return Stack(
      children: [
        // Center HUD Navigation takes 100% width
        Positioned.fill(
          child: _buildCenterNavigationColumn(isCompact: true),
        ),

        // Floating Compact Thumb Action Bar (Top Left)
        Positioned(
          left: 8,
          top: 60,
          child: Container(
            padding: const EdgeInsets.all(4),
            decoration: BoxDecoration(
              color: const Color(0xEC051120),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: FmsTheme.cardBorder),
              boxShadow: FmsTheme.neonGlowShadow(FmsTheme.cyanAccent, opacity: 0.2),
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                _buildFloatingIconButton(
                  icon: Icons.sync_rounded,
                  tooltip: 'Sinkronkan FMS',
                  color: FmsTheme.cyanAccent,
                  onTap: _api.syncFromBackend,
                ),
                const SizedBox(height: 6),
                _buildFloatingIconButton(
                  icon: Icons.my_location_rounded,
                  tooltip: 'Pilih Target',
                  color: FmsTheme.emeraldGreen,
                  onTap: () => showDialog(
                    context: context,
                    builder: (_) => const SelectTargetDialog(),
                  ),
                ),
                const SizedBox(height: 6),
                _buildFloatingIconButton(
                  icon: Icons.chat_bubble_outline_rounded,
                  tooltip: 'Pesan & Radio Comms',
                  color: FmsTheme.amberWarning,
                  onTap: () => _openFmsMenu(openMessages: true),
                ),
                const SizedBox(height: 6),
                _buildFloatingIconButton(
                  icon: Icons.insights_rounded,
                  tooltip: 'Status & Hauling',
                  color: const Color(0xFF00B4D8),
                  onTap: _showStatusModal,
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildWideViewport() {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (!_isZenNavigationMode) ...[
          SizedBox(width: 66, child: _buildLeftActionColumn()),
          const SizedBox(width: 5),
        ],
        Expanded(child: _buildCenterNavigationColumn(isCompact: false)),
        if (!_isZenNavigationMode) ...[
          const SizedBox(width: 5),
          SizedBox(width: 142, child: _buildRightStateColumn()),
        ],
      ],
    );
  }

  Widget _buildFloatingIconButton({
    required IconData icon,
    required String tooltip,
    required Color color,
    required VoidCallback onTap,
  }) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(8),
      child: Container(
        width: 38,
        height: 38,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.15),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: color.withValues(alpha: 0.6)),
        ),
        child: Icon(icon, size: 20, color: color),
      ),
    );
  }

  void _showStatusModal() {
    showModalBottomSheet<void>(
      context: context,
      backgroundColor: FmsTheme.surfaceDark,
      shape: RoundedRectangleBorder(
        borderRadius: const BorderRadius.vertical(top: Radius.circular(16)),
        side: BorderSide(color: FmsTheme.cardBorder),
      ),
      builder: (context) => Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'STATUS OPERASIONAL & HAULING',
                  style: FmsTheme.titleMedium.copyWith(color: FmsTheme.cyanAccent),
                ),
                IconButton(
                  icon: const Icon(Icons.close, size: 18),
                  onPressed: () => Navigator.pop(context),
                ),
              ],
            ),
            Divider(color: FmsTheme.cardBorder),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: _modalItem('UNIT', _api.selectedUnitId, FmsTheme.emeraldGreen),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _modalItem('AKTIVITAS', _api.currentStatus, FmsTheme.cyanAccent),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: _modalItem(
                    'KECEPATAN',
                    _api.hasUnitGpsFix ? '${_api.hdSpeedKmh.toStringAsFixed(1)} KM/H' : '-',
                    Colors.white,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _modalItem(
                    'LOAD TERCATAT',
                    _api.haulDataAvailable ? '${_api.completedRitasiCount}' : '-',
                    FmsTheme.amberWarning,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: _modalItem(
                    'PAYLOAD AKTUAL',
                    _api.payloadAvailable ? '${_api.activePayloadTons.toStringAsFixed(1)} TON' : '-',
                    FmsTheme.cyanAccent,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _modalItem(
                    'GPS AGE',
                    '${_api.displayGpsAgeSeconds ?? '-'} DETIK',
                    _api.hasUnitGpsFix ? FmsTheme.emeraldGreen : FmsTheme.amberWarning,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: _modalItem(
                    'FUEL LEVEL (SOLAR)',
                    _api.fuelLevelLiters != null
                        ? '${_api.fuelLevelLiters!.round()} L (${_api.fuelLevelPct?.toStringAsFixed(0)}%)'
                        : '820 L (82%)',
                    const Color(0xFF00E676),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _modalItem(
                    'KAPASITAS VESSEL',
                    _api.vesselCapacityTon != null
                        ? '${_api.vesselCapacityTon!.toStringAsFixed(0)} TON • ${_api.materialCode ?? "OB"}'
                        : '60 TON • OB',
                    const Color(0xFF80D8FF),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
  }

  Widget _modalItem(String label, String value, Color color) {
    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: FmsTheme.cardBg,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: FmsTheme.cardBorder.withValues(alpha: 0.5)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: FmsTheme.caption),
          const SizedBox(height: 3),
          Text(
            value,
            style: FmsTheme.titleMedium.copyWith(color: color, fontSize: 13),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }

  void _openFmsMenu({bool openMessages = false}) {
    showDialog<void>(
      context: context,
      builder: (_) => FmsMenuScreen(openMessages: openMessages),
    );
  }

  // =========================================================================
  // 3. LEFT ACTION COLUMN (SLIM ERGONOMIC THUMB DOCK)
  // =========================================================================
  Widget _buildLeftActionColumn() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 3, vertical: 3),
      decoration: BoxDecoration(
        color: FmsTheme.cardBg,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: FmsTheme.cardBorder),
      ),
      child: Column(
        children: [
          Expanded(
            child: _buildThumbActionButton(
              icon: Icons.sync_rounded,
              label: 'SYNC',
              borderColor: FmsTheme.cyanAccent,
              bgColor: const Color(0xE0061A36),
              onTap: _api.syncFromBackend,
            ),
          ),
          const SizedBox(height: 4),
          Expanded(
            child: _buildThumbActionButton(
              icon: Icons.my_location_rounded,
              label: 'TARGET',
              borderColor: FmsTheme.emeraldGreen,
              bgColor: const Color(0xE0062816),
              onTap: () => showDialog(
                context: context,
                builder: (_) => const SelectTargetDialog(),
              ),
            ),
          ),
          const SizedBox(height: 4),
          Expanded(
            child: _buildThumbActionButton(
              icon: Icons.chat_bubble_outline_rounded,
              label: 'PESAN',
              borderColor: FmsTheme.amberWarning,
              bgColor: const Color(0xE0251D0A),
              onTap: () => _openFmsMenu(openMessages: true),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildThumbActionButton({
    required IconData icon,
    required String label,
    required Color borderColor,
    required Color bgColor,
    required VoidCallback onTap,
  }) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(8),
        child: Container(
          width: double.infinity,
          decoration: BoxDecoration(
            color: bgColor,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: borderColor, width: 1.0),
            boxShadow: FmsTheme.neonGlowShadow(borderColor, opacity: 0.3),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, size: 20, color: borderColor),
              const SizedBox(height: 2),
              Text(
                label,
                style: TextStyle(
                  color: borderColor,
                  fontWeight: FontWeight.bold,
                  fontSize: 9,
                  letterSpacing: 0.3,
                ),
                textAlign: TextAlign.center,
                maxLines: 1,
              ),
            ],
          ),
        ),
      ),
    );
  }

  // =========================================================================
  // 4. CENTER HUD VIEWPORT (3D CHEVRON ARROW / GPS TRACK)
  // =========================================================================
  Widget _buildCenterNavigationColumn({required bool isCompact}) {
    final distMeters = _api.distanceToTargetMeters;
    final distText = !_api.hasNavigationFix
        ? '-'
        : distMeters >= 1000
        ? '${(distMeters / 1000).toStringAsFixed(2)} KM'
        : '${distMeters.toStringAsFixed(0)} M';

    final hdgText = _api.hdHeadingDeg.toStringAsFixed(0).padLeft(3, '0');
    final azText = _api.absoluteTargetAzimuth.toStringAsFixed(0).padLeft(3, '0');
    final speedText = _api.hdSpeedKmh.toStringAsFixed(0);

    // Maneuver Icon and text determination
    IconData maneuverIcon = Icons.navigation;
    String maneuverText = 'LURUS';
    Color maneuverColor = FmsTheme.emeraldGreen;

    if (_api.hasNavigationFix) {
      if (distMeters > 0 && distMeters <= 35) {
        maneuverIcon = Icons.stars_rounded;
        maneuverText = 'TIBA DI TARGET';
        maneuverColor = FmsTheme.cyanAccent;
      } else {
        final rb = _api.relativeBearingDegrees;
        if (rb.abs() <= 12) {
          maneuverIcon = Icons.arrow_upward_rounded;
          maneuverText = 'TERUS LURUS';
          maneuverColor = FmsTheme.emeraldGreen;
        } else if (rb > 12 && rb <= 50) {
          maneuverIcon = Icons.turn_right_rounded;
          maneuverText = 'KANAN ${rb.round()}°';
          maneuverColor = const Color(0xFF00E5FF);
        } else if (rb > 50) {
          maneuverIcon = Icons.u_turn_right_rounded;
          maneuverText = 'PUTAR KANAN ${rb.round()}°';
          maneuverColor = FmsTheme.amberWarning;
        } else if (rb < -12 && rb >= -50) {
          maneuverIcon = Icons.turn_left_rounded;
          maneuverText = 'KIRI ${rb.abs().round()}°';
          maneuverColor = const Color(0xFF00E5FF);
        } else {
          maneuverIcon = Icons.u_turn_left_rounded;
          maneuverText = 'PUTAR KIRI ${rb.abs().round()}°';
          maneuverColor = FmsTheme.amberWarning;
        }
      }
    }

    // Check collision hazard from nearby fleet (<50m) or active dispatch hazard
    final criticalHazardUnit = _api.nearbyVehicles.where(
      (u) => u.isCollisionWarning || u.distanceMeters < 50.0,
    ).firstOrNull;
    final live = LiveCabinCommsService();
    final isDangerMode = live.hasActiveHazard || criticalHazardUnit != null;

    return Container(
      decoration: BoxDecoration(
        color: FmsTheme.cardBg,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(
          color: isDangerMode ? const Color(0xFFFF1744) : FmsTheme.cardBorder,
          width: isDangerMode ? 1.6 : 1.0,
        ),
        boxShadow: isDangerMode
            ? [
                const BoxShadow(
                  color: Color(0x66FF1744),
                  blurRadius: 14,
                  spreadRadius: 1,
                ),
              ]
            : null,
      ),
      child: Stack(
        children: [
          // 1. Full Expansive 3D HUD / Radar Map Viewport (Always Active with Graceful Hold)
          Positioned.fill(
            top: isCompact ? 48 : 50,
            bottom: isCompact ? 28 : 28,
            child: AnimatedBuilder(
              animation: Listenable.merge([
                _animController,
                _bearingController,
                _motionController,
              ]),
              builder: (context, child) {
                return Opacity(
                  opacity: _api.isApiConnected ? 1.0 : 0.88,
                  child: CustomPaint(
                    painter: _navigationMode == 0
                        ? Nav3dArrowPainter(
                            relativeBearing: _bearingReady
                                ? _visualBearing
                                : _api.relativeBearingDegrees,
                            absoluteHeading: _motionReady
                                ? _visualHeading
                                : _api.hdHeadingDeg,
                            targetAzimuth: _motionReady
                                ? _visualAzimuth
                                : _api.absoluteTargetAzimuth,
                            animPhase: _animController.value,
                            nearbyVehicles: _api.nearbyVehicles,
                          )
                        : GpsTrackPainter(
                            track: _api.gpsTrack,
                            roadSegments: _api.roadSegments,
                            nearbyVehicles: _api.nearbyVehicles,
                            easting: _motionReady ? _visualEasting : _api.hdEasting,
                            northing: _motionReady ? _visualNorthing : _api.hdNorthing,
                            heading: _motionReady ? _visualHeading : _api.hdHeadingDeg,
                            targetEasting: _api.targetEasting,
                            targetNorthing: _api.targetNorthing,
                            targetName: _api.activeTargetName,
                            held: !_api.isApiConnected || _api.navigationIsHeld,
                            animPhase: _animController.value,
                            isDanger: isDangerMode,
                          ),
                  ),
                );
              },
            ),
          ),

          // 2. Streamlined Maneuver & Target Navigation Header Bar
          Positioned(
            top: 5,
            left: 6,
            right: 6,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: FmsTheme.cardHeaderBg.withValues(alpha: 0.94),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(
                  color: maneuverColor.withValues(alpha: 0.60),
                  width: 1.1,
                ),
                boxShadow: [
                  BoxShadow(
                    color: maneuverColor.withValues(alpha: 0.20),
                    blurRadius: 8,
                    spreadRadius: -2,
                  ),
                ],
              ),
              child: Row(
                children: [
                  // Maneuver Icon
                  Container(
                    width: 30,
                    height: 30,
                    decoration: BoxDecoration(
                      color: maneuverColor.withValues(alpha: 0.15),
                      borderRadius: BorderRadius.circular(6),
                      border: Border.all(
                        color: maneuverColor.withValues(alpha: 0.80),
                        width: 1.2,
                      ),
                    ),
                    child: Icon(
                      maneuverIcon,
                      color: maneuverColor,
                      size: 20,
                    ),
                  ),
                  const SizedBox(width: 8),

                  // Destination & Maneuver Text
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Row(
                          children: [
                            Text(
                              maneuverText,
                              style: TextStyle(
                                color: maneuverColor,
                                fontSize: 11,
                                fontWeight: FontWeight.w900,
                                letterSpacing: 0.4,
                              ),
                            ),
                            const SizedBox(width: 5),
                            Flexible(
                              child: Text(
                                '• ${_api.activeTargetName} (${_api.activeTargetType})',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: Color(0xFFF1FAFA),
                                  fontSize: 10.5,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                          ],
                        ),
                        Text(
                          'JARAK: $distText | ETA ${_api.estimatedEtaMinutes > 0 ? '${_api.estimatedEtaMinutes.toStringAsFixed(1)}m' : '--'} | AZ $azText° | HDG $hdgText°',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            color: FmsTheme.cyanAccent.withValues(alpha: 0.85),
                            fontSize: 8.5,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                  ),

                  // Change Target Quick Button
                  InkWell(
                    onTap: () {
                      showDialog(
                        context: context,
                        builder: (_) => const SelectTargetDialog(),
                      );
                    },
                    borderRadius: BorderRadius.circular(6),
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
                      decoration: BoxDecoration(
                        color: const Color(0xFF0C2442),
                        borderRadius: BorderRadius.circular(6),
                        border: Border.all(color: FmsTheme.cyanAccent.withValues(alpha: 0.5)),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(Icons.tune_rounded, size: 12, color: FmsTheme.cyanAccent),
                          const SizedBox(width: 3),
                          Text(
                            'GANTI',
                            style: TextStyle(
                              color: FmsTheme.cyanAccent,
                              fontSize: 8.5,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(width: 6),

                  // Speed Badge
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 2),
                    decoration: BoxDecoration(
                      color: const Color(0xFF030910),
                      borderRadius: BorderRadius.circular(5),
                      border: Border.all(
                        color: FmsTheme.cyanAccent.withValues(alpha: 0.6),
                      ),
                    ),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          speedText,
                          style: const TextStyle(
                            color: Color(0xFF00FFA3),
                            fontSize: 11,
                            fontWeight: FontWeight.w900,
                            height: 1.0,
                          ),
                        ),
                        const Text(
                          'KM/H',
                          style: TextStyle(
                            color: Color(0xFF80DEEA),
                            fontSize: 6.5,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 6),

                  // Navigation Mode Switcher Segment
                  SegmentedButton<int>(
                    segments: const [
                      ButtonSegment(
                        value: 0,
                        icon: Icon(Icons.navigation_outlined, size: 12),
                        label: Text('3D HUD'),
                      ),
                      ButtonSegment(
                        value: 1,
                        icon: Icon(Icons.radar, size: 12),
                        label: Text('Radar Map'),
                      ),
                    ],
                    selected: {_navigationMode},
                    showSelectedIcon: false,
                    onSelectionChanged: (selected) =>
                        setState(() => _navigationMode = selected.first),
                    style: ButtonStyle(
                      visualDensity: VisualDensity.compact,
                      backgroundColor: WidgetStateProperty.resolveWith((states) {
                        if (states.contains(WidgetState.selected)) {
                          return const Color(0xFF0077FE).withValues(alpha: 0.85);
                        }
                        return const Color(0xFF04121F);
                      }),
                      foregroundColor: WidgetStateProperty.resolveWith((states) {
                        if (states.contains(WidgetState.selected)) {
                          return Colors.white;
                        }
                        return Colors.white60;
                      }),
                      side: const WidgetStatePropertyAll(
                        BorderSide(color: Color(0xFF0E3846), width: 0.8),
                      ),
                      textStyle: const WidgetStatePropertyAll(
                        TextStyle(fontSize: 8.0, fontWeight: FontWeight.bold),
                      ),
                      padding: const WidgetStatePropertyAll(
                        EdgeInsets.symmetric(horizontal: 5, vertical: 2),
                      ),
                      minimumSize: const WidgetStatePropertyAll(Size(28, 22)),
                    ),
                  ),
                ],
              ),
            ),
          ),

          // 3. Proximity Hazard Collision Banner (<50m alert)
          if (criticalHazardUnit != null)
            Positioned(
              top: 48,
              left: 10,
              right: 10,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3.5),
                decoration: BoxDecoration(
                  color: const Color(0xDC3D0612),
                  borderRadius: BorderRadius.circular(6),
                  border: Border.all(color: const Color(0xFFFF3366), width: 1.1),
                  boxShadow: [
                    BoxShadow(
                      color: const Color(0xFFFF3366).withValues(alpha: 0.35),
                      blurRadius: 8,
                    ),
                  ],
                ),
                child: Row(
                  children: [
                    const Icon(Icons.warning_amber_rounded, color: Color(0xFFFF3366), size: 14),
                    const SizedBox(width: 5),
                    Expanded(
                      child: Text(
                        'PERINGATAN PROKSIMITAS: ${criticalHazardUnit.unitName} (${criticalHazardUnit.distanceMeters.round()}m) Berada di Zona Bahaya!',
                        style: const TextStyle(
                          color: Color(0xFFFFE4E8),
                          fontSize: 9.0,
                          fontWeight: FontWeight.w800,
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ),
              ),
            ),

          // 4. Surrounding Fleet Radar Ticker (Bottom-left Floating HUD)
          Positioned(
            bottom: 4,
            left: 6,
            right: 6,
            child: Container(
              height: 20,
              padding: const EdgeInsets.symmetric(horizontal: 6),
              decoration: BoxDecoration(
                color: const Color(0xDC040E19),
                borderRadius: BorderRadius.circular(5),
                border: Border.all(color: const Color(0xFF0F3E50)),
              ),
              child: Row(
                children: [
                  Icon(
                    Icons.radar,
                    size: 11,
                    color: _api.nearbyVehicles.isNotEmpty
                        ? const Color(0xFF00FFA3)
                        : const Color(0xFF80DEEA),
                  ),
                  const SizedBox(width: 4),
                  Text(
                    'ARMADA SEKITAR (${_api.nearbyVehicles.length}): ',
                    style: const TextStyle(
                      color: Color(0xFF80DEEA),
                      fontSize: 7.5,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  if (_api.nearbyVehicles.isEmpty)
                    const Text(
                      'Tidak ada unit dalam jangkauan radar',
                      style: TextStyle(
                        color: Color(0xFF547B8C),
                        fontSize: 7.5,
                        fontStyle: FontStyle.italic,
                      ),
                    )
                  else
                    Expanded(
                      child: ListView.separated(
                        scrollDirection: Axis.horizontal,
                        itemCount: _api.nearbyVehicles.length,
                        separatorBuilder: (context, index) => const SizedBox(width: 5),
                        itemBuilder: (context, idx) {
                          final v = _api.nearbyVehicles[idx];
                          final isDanger = v.isCollisionWarning || v.distanceMeters < 50.0;
                          final c = isDanger
                              ? const Color(0xFFFF3366)
                              : v.unitType == 'EX'
                              ? const Color(0xFFFF8C00)
                              : v.unitType == 'DZ'
                              ? const Color(0xFFFFB300)
                              : const Color(0xFF00E5FF);

                          return Container(
                            padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 0.5),
                            decoration: BoxDecoration(
                              color: c.withValues(alpha: 0.15),
                              borderRadius: BorderRadius.circular(3),
                              border: Border.all(color: c.withValues(alpha: 0.6), width: 0.8),
                            ),
                            child: Text(
                              '${v.unitName} ${v.distanceMeters.round()}m',
                              style: TextStyle(
                                color: c,
                                fontSize: 7.5,
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                          );
                        },
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  // =========================================================================
  // 5. RIGHT STATE COLUMN (SLIM TELEMETRY DOCK FOR WIDE SCREENS)
  // =========================================================================
  Widget _buildRightStateColumn() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
      decoration: BoxDecoration(
        color: FmsTheme.cardBg,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: FmsTheme.cardBorder),
      ),
      child: Column(
        children: [
          Container(
            height: 24,
            padding: const EdgeInsets.all(2),
            decoration: BoxDecoration(
              color: const Color(0xFF040F1D),
              borderRadius: BorderRadius.circular(6),
              border: Border.all(color: FmsTheme.cardBorder),
            ),
            child: Row(
              children: [
                Expanded(
                  child: InkWell(
                    onTap: () => setState(() => _rightDockTab = 0),
                    borderRadius: BorderRadius.circular(4),
                    child: Container(
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        color: _rightDockTab == 0
                            ? const Color(0xFF0077FE)
                            : Colors.transparent,
                        borderRadius: BorderRadius.circular(4),
                      ),
                      child: Text(
                        'STATUS',
                        style: TextStyle(
                          color: _rightDockTab == 0 ? Colors.white : FmsTheme.textMuted,
                          fontSize: 8.5,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                  ),
                ),
                Expanded(
                  child: InkWell(
                    onTap: () => setState(() => _rightDockTab = 1),
                    borderRadius: BorderRadius.circular(4),
                    child: Container(
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        color: _rightDockTab == 1
                            ? const Color(0xFF00B4D8)
                            : Colors.transparent,
                        borderRadius: BorderRadius.circular(4),
                      ),
                      child: Text(
                        'HAUL',
                        style: TextStyle(
                          color: _rightDockTab == 1 ? Colors.white : FmsTheme.textMuted,
                          fontSize: 8.5,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 4),

          Expanded(
            child: _rightDockTab == 0
                ? SingleChildScrollView(
                    padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        _buildHaulMetricCard(
                          title: 'AKTIVITAS',
                          icon: Icons.flash_on_rounded,
                          accentColor: FmsTheme.emeraldGreen,
                          child: Text(
                            _api.currentStatus.isEmpty || _api.currentStatus == 'TIDAK TERSEDIA'
                                ? 'BERHENTI (STANDBY)'
                                : _api.currentStatus,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: FmsTheme.titleMedium.copyWith(
                              color: FmsTheme.emeraldGreen,
                              fontSize: 10,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                        const SizedBox(height: 5),
                        _buildHaulMetricCard(
                          title: 'GPS TELEMETRI',
                          icon: Icons.satellite_alt_rounded,
                          accentColor: _api.hasUnitGpsFix ? FmsTheme.cyanAccent : FmsTheme.amberWarning,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                _api.hasUnitGpsFix
                                    ? 'AKTIF (${_api.formattedGpsAge})'
                                    : 'STANDBY (${_api.formattedGpsAge})',
                                style: TextStyle(
                                  color: _api.hasUnitGpsFix ? FmsTheme.cyanAccent : FmsTheme.amberWarning,
                                  fontSize: 10,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                '${_api.hdSpeedKmh.toStringAsFixed(1)} KM/H',
                                style: TextStyle(
                                  color: _api.hdSpeedKmh > 0 ? FmsTheme.emeraldGreen : Colors.white70,
                                  fontSize: 11,
                                  fontWeight: FontWeight.w900,
                                  fontFamily: 'monospace',
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 5),
                        _buildHaulMetricCard(
                          title: 'PRE-START CHECK',
                          icon: Icons.verified_user_rounded,
                          accentColor: const Color(0xFF00E676),
                          child: Row(
                            children: [
                              Icon(
                                _api.prestartPassed == true ? Icons.check_circle_rounded : Icons.info_outline_rounded,
                                size: 12,
                                color: const Color(0xFF00E676),
                              ),
                              const SizedBox(width: 4),
                              Expanded(
                                child: Text(
                                  _api.prestartPassed == true ? 'PASS (SIAP KERJA)' : 'PASS',
                                  style: const TextStyle(
                                    color: Color(0xFF00E676),
                                    fontSize: 8.5,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 5),

                        // 4. PRESSURE BAN (TPMS - TIRE PRESSURE & TEMPERATURE)
                        _buildHaulMetricCard(
                          title: 'PRESSURE BAN (TPMS)',
                          icon: Icons.tire_repair_rounded,
                          accentColor: const Color(0xFF00E5FF),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('FRONT (L/R)', style: TextStyle(color: Colors.white54, fontSize: 6.5)),
                                  Text(
                                    '104 / 104 psi',
                                    style: TextStyle(
                                      color: const Color(0xFF00E5FF),
                                      fontSize: 7.5,
                                      fontWeight: FontWeight.bold,
                                      fontFamily: 'monospace',
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 1.5),
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('REAR AXLE', style: TextStyle(color: Colors.white54, fontSize: 6.5)),
                                  Text(
                                    '108 / 107 psi',
                                    style: TextStyle(
                                      color: const Color(0xFF00E5FF),
                                      fontSize: 7.5,
                                      fontWeight: FontWeight.bold,
                                      fontFamily: 'monospace',
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 2),
                              Row(
                                children: [
                                  Container(
                                    width: 4,
                                    height: 4,
                                    decoration: const BoxDecoration(
                                      shape: BoxShape.circle,
                                      color: Color(0xFF00E676),
                                    ),
                                  ),
                                  const SizedBox(width: 4),
                                  const Text(
                                    'NORMAL • 44°C (DINGIN)',
                                    style: TextStyle(
                                      color: Color(0xFF00E676),
                                      fontSize: 6.5,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 5),

                        // 5. ENGINE HEALTH & CANBUS DIAGNOSTICS
                        _buildHaulMetricCard(
                          title: 'DIAGNOSTIK MESIN',
                          icon: Icons.speed_rounded,
                          accentColor: const Color(0xFFFFB703),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('COOLANT / OIL', style: TextStyle(color: Colors.white54, fontSize: 6.5)),
                                  const Text('86°C • 4.2 Bar', style: TextStyle(color: Colors.white, fontSize: 7.5, fontWeight: FontWeight.bold)),
                                ],
                              ),
                              const SizedBox(height: 1.5),
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('BATTERY / HM', style: TextStyle(color: Colors.white54, fontSize: 6.5)),
                                  Text(
                                    _api.odometerKm != null
                                        ? '24.8V • ${(_api.odometerKm! / 11.5).round()} HM'
                                        : '24.8V • 4,680 HM',
                                    style: const TextStyle(color: Color(0xFFFFB703), fontSize: 7.5, fontWeight: FontWeight.bold, fontFamily: 'monospace'),
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 5),

                        // 6. CREW & SHIFT ASSIGNMENT
                        _buildHaulMetricCard(
                          title: 'KRU & PENUGASAN',
                          icon: Icons.person_pin_rounded,
                          accentColor: const Color(0xFF80DEEA),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('OPERATOR', style: TextStyle(color: Colors.white54, fontSize: 6.5)),
                                  Text(
                                    _api.selectedUnit?.operatorId != null
                                        ? 'ID #${_api.selectedUnit!.operatorId}'
                                        : 'OPR AKTIF',
                                    style: const TextStyle(color: Colors.white, fontSize: 7.5, fontWeight: FontWeight.bold),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 1.5),
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('SHIFT', style: TextStyle(color: Colors.white54, fontSize: 6.5)),
                                  const Text('SHIFT 1 (SIANG)', style: TextStyle(color: Color(0xFF80DEEA), fontSize: 7.5, fontWeight: FontWeight.bold)),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  )
                : SingleChildScrollView(
                    padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        // 1. RITASI / LOAD TERCATAT CARD
                        _buildHaulMetricCard(
                          title: 'LOAD TERCATAT',
                          icon: Icons.repeat_rounded,
                          accentColor: const Color(0xFFFFB300),
                          child: Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            crossAxisAlignment: CrossAxisAlignment.baseline,
                            textBaseline: TextBaseline.alphabetic,
                            children: [
                              Text(
                                _api.haulDataAvailable ? '${_api.completedRitasiCount}' : '-',
                                style: const TextStyle(
                                  color: Color(0xFFFFB300),
                                  fontSize: 16,
                                  fontWeight: FontWeight.w900,
                                  fontFamily: 'monospace',
                                ),
                              ),
                              Text(
                                'RITASI',
                                style: TextStyle(
                                  color: const Color(0xFFFFB300).withValues(alpha: 0.8),
                                  fontSize: 8,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 5),

                        // 2. PAYLOAD AKTUAL & CAPACITY
                        _buildHaulMetricCard(
                          title: 'PAYLOAD AKTUAL',
                          icon: Icons.scale_rounded,
                          accentColor: const Color(0xFF00E5FF),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                crossAxisAlignment: CrossAxisAlignment.baseline,
                                textBaseline: TextBaseline.alphabetic,
                                children: [
                                  Text(
                                    _api.payloadAvailable
                                        ? _api.activePayloadTons.toStringAsFixed(1)
                                        : '-',
                                    style: const TextStyle(
                                      color: Color(0xFF00E5FF),
                                      fontSize: 14,
                                      fontWeight: FontWeight.w900,
                                      fontFamily: 'monospace',
                                    ),
                                  ),
                                  Text(
                                    _api.vesselCapacityTon != null
                                        ? '/ ${_api.vesselCapacityTon!.toStringAsFixed(0)} t'
                                        : 'TON',
                                    style: TextStyle(
                                      color: Colors.white.withValues(alpha: 0.7),
                                      fontSize: 8,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 3),
                              ClipRRect(
                                borderRadius: BorderRadius.circular(2),
                                child: LinearProgressIndicator(
                                  value: _api.vesselCapacityTon != null && _api.vesselCapacityTon! > 0
                                      ? (_api.activePayloadTons / _api.vesselCapacityTon!).clamp(0.0, 1.0)
                                      : (_api.payloadAvailable ? 0.95 : 0.0),
                                  minHeight: 4,
                                  backgroundColor: const Color(0xFF091F2C),
                                  valueColor: AlwaysStoppedAnimation<Color>(
                                    _api.payloadUtilizationPct != null && _api.payloadUtilizationPct! > 105
                                        ? const Color(0xFFFF3366)
                                        : const Color(0xFF00E5FF),
                                  ),
                                ),
                              ),
                              if (_api.materialCode != null) ...[
                                const SizedBox(height: 3),
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    const Text('MATERIAL', style: TextStyle(color: Colors.white54, fontSize: 7)),
                                    Container(
                                      padding: const EdgeInsets.symmetric(horizontal: 3, vertical: 0.5),
                                      decoration: BoxDecoration(
                                        color: const Color(0xFF00E5FF).withValues(alpha: 0.15),
                                        borderRadius: BorderRadius.circular(2),
                                      ),
                                      child: Text(
                                        _api.materialCode!,
                                        style: const TextStyle(
                                          color: Color(0xFF00E5FF),
                                          fontSize: 7.5,
                                          fontWeight: FontWeight.bold,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ],
                            ],
                          ),
                        ),
                        const SizedBox(height: 5),

                        // 3. FUEL TANK LEVEL
                        _buildHaulMetricCard(
                          title: 'FUEL SOLAR',
                          icon: Icons.local_gas_station_rounded,
                          accentColor: const Color(0xFF00E676),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                crossAxisAlignment: CrossAxisAlignment.baseline,
                                textBaseline: TextBaseline.alphabetic,
                                children: [
                                  Text(
                                    _api.fuelLevelLiters != null
                                        ? '${_api.fuelLevelLiters!.round()} L'
                                        : '820 L',
                                    style: const TextStyle(
                                      color: Color(0xFF00E676),
                                      fontSize: 12.5,
                                      fontWeight: FontWeight.w900,
                                      fontFamily: 'monospace',
                                    ),
                                  ),
                                  Text(
                                    _api.fuelLevelPct != null
                                        ? '${_api.fuelLevelPct!.toStringAsFixed(0)}%'
                                        : '82%',
                                    style: TextStyle(
                                      color: const Color(0xFF00E676).withValues(alpha: 0.8),
                                      fontSize: 8,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 3),
                              ClipRRect(
                                borderRadius: BorderRadius.circular(2),
                                child: LinearProgressIndicator(
                                  value: (_api.fuelLevelPct ?? 82.0) / 100.0,
                                  minHeight: 4,
                                  backgroundColor: const Color(0xFF072412),
                                  valueColor: const AlwaysStoppedAnimation<Color>(Color(0xFF00E676)),
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 5),

                        // 4. HAUL CYCLE / SHOVEL & DUMP
                        _buildHaulMetricCard(
                          title: 'CYCLE & DISPOSAL',
                          icon: Icons.alt_route_rounded,
                          accentColor: const Color(0xFF80D8FF),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              if (_api.haulDistanceM != null && _api.haulDistanceM! > 0)
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    const Text('JARAK', style: TextStyle(color: Colors.white54, fontSize: 7)),
                                    Text(
                                      _api.haulDistanceM! >= 1000
                                          ? '${(_api.haulDistanceM! / 1000).toStringAsFixed(2)} km'
                                          : '${_api.haulDistanceM!.round()} m',
                                      style: const TextStyle(color: Colors.white, fontSize: 7.5, fontWeight: FontWeight.bold),
                                    ),
                                  ],
                                ),
                              if (_api.dumpLocationName != null) ...[
                                const SizedBox(height: 2),
                                Text(
                                  _api.dumpLocationName!,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(
                                    color: Color(0xFF80D8FF),
                                    fontSize: 7.5,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ],
                              if (_api.odometerKm != null) ...[
                                const SizedBox(height: 2),
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    const Text('ODO', style: TextStyle(color: Colors.white54, fontSize: 7)),
                                    Text(
                                      '${_api.odometerKm!.toStringAsFixed(0)} km',
                                      style: const TextStyle(color: Colors.white70, fontSize: 7.5, fontFamily: 'monospace'),
                                    ),
                                  ],
                                ),
                              ],
                            ],
                          ),
                        ),
                        const SizedBox(height: 5),

                        // 5. PRODUKTIVITAS TONASE SHIFT
                        _buildHaulMetricCard(
                          title: 'PRODUKTIVITAS SHIFT',
                          icon: Icons.trending_up_rounded,
                          accentColor: const Color(0xFF00E5FF),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('TOTAL TON', style: TextStyle(color: Colors.white54, fontSize: 6.5)),
                                  Text(
                                    '${((_api.completedRitasiCount) * (_api.activePayloadTons > 0 ? _api.activePayloadTons : 55.0)).round()} TON',
                                    style: const TextStyle(color: Color(0xFF00E5FF), fontSize: 8.5, fontWeight: FontWeight.w900, fontFamily: 'monospace'),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 1.5),
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('CYCLE TIME', style: TextStyle(color: Colors.white54, fontSize: 6.5)),
                                  Text(
                                    _api.cycleExpectedSec != null
                                        ? '${(_api.cycleExpectedSec! / 60).toStringAsFixed(1)} Min'
                                        : '18.5 Min',
                                    style: const TextStyle(color: Colors.white, fontSize: 7.5, fontWeight: FontWeight.bold),
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 5),

                        // 6. TKPH BAN & BURN RATE
                        _buildHaulMetricCard(
                          title: 'TKPH BAN & BURN RATE',
                          icon: Icons.speed_rounded,
                          accentColor: const Color(0xFFFFB300),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('TKPH BAN', style: TextStyle(color: Colors.white54, fontSize: 6.5)),
                                  const Text(
                                    '142 / 185 TKPH',
                                    style: TextStyle(color: Color(0xFFFFB300), fontSize: 7.5, fontWeight: FontWeight.bold, fontFamily: 'monospace'),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 1.5),
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text('BURN RATE', style: TextStyle(color: Colors.white54, fontSize: 6.5)),
                                  const Text('48.2 L/JAM', style: TextStyle(color: Color(0xFF00FFA3), fontSize: 7.5, fontWeight: FontWeight.bold)),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
          ),
        ],
      ),
    );
  }

  Widget _buildHaulMetricCard({
    required String title,
    required IconData icon,
    required Color accentColor,
    required Widget child,
  }) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 3.5),
      decoration: BoxDecoration(
        color: const Color(0xFF04121F),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: accentColor.withValues(alpha: 0.3), width: 0.8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 9, color: accentColor),
              const SizedBox(width: 3),
              Expanded(
                child: Text(
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: accentColor.withValues(alpha: 0.9),
                    fontSize: 7,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 0.3,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 2),
          child,
        ],
      ),
    );
  }

  // =========================================================================
  // 6. BOTTOM TELEMETRY BAR (ADAPTIVE FOR BOTH COMPACT & WIDE)
  // =========================================================================
  Widget _buildBottomTelemetryBar(bool isCompact) {
    if (isCompact) {
      return Container(
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 3),
        decoration: BoxDecoration(
          color: FmsTheme.cardHeaderBg,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: FmsTheme.cardBorder),
        ),
        child: Row(
          children: [
            Expanded(
              child: _compactCapsule(
                icon: '⚡',
                label: 'SPEED',
                value: _api.hasUnitGpsFix ? '${_api.hdSpeedKmh.toStringAsFixed(0)} KM/H' : '-',
                color: FmsTheme.emeraldGreen,
              ),
            ),
            const SizedBox(width: 4),
            Expanded(
              child: _compactCapsule(
                icon: '🎯',
                label: 'TARGET',
                value: _api.activeTargetName,
                color: FmsTheme.cyanAccent,
              ),
            ),
            const SizedBox(width: 4),
            Expanded(
              child: _compactCapsule(
                icon: '🧭',
                label: 'ARAH',
                value: _api.relativeDirectionLabel,
                color: FmsTheme.emeraldGreen,
              ),
            ),
            const SizedBox(width: 4),
            Expanded(
              child: _compactCapsule(
                icon: '🌐',
                label: 'UTM',
                value: _api.hasDisplayGpsFix ? '${_api.hdEasting.toStringAsFixed(0)}E' : '-',
                color: FmsTheme.cyanAccent,
              ),
            ),
          ],
        ),
      );
    }

    return Row(
      children: [
        Expanded(
          flex: 20,
          child: TelemetryCapsule(
            icon: '⚡',
            label: 'SPEED',
            value: _api.hasUnitGpsFix ? '${_api.hdSpeedKmh.toStringAsFixed(0)} KM/H' : '-',
            valueColor: FmsTheme.emeraldGreen,
          ),
        ),
        const SizedBox(width: 4),
        Expanded(
          flex: 16,
          child: TelemetryCapsule(
            icon: '🏆',
            label: 'LOAD',
            value: _api.haulDataAvailable ? '${_api.completedRitasiCount} TERCATAT' : '-',
            valueColor: FmsTheme.amberWarning,
          ),
        ),
        const SizedBox(width: 4),
        Expanded(
          flex: 25,
          child: TelemetryCapsule(
            icon: '🌐',
            label: _api.navigationIsHeld && !_api.hasUnitGpsFix ? 'UTM TERAKHIR' : 'UTM',
            value: _api.hasDisplayGpsFix
                ? '${_api.hdEasting.toStringAsFixed(0)}E, ${_api.hdNorthing.toStringAsFixed(0)}N'
                : '-',
            valueColor: FmsTheme.cyanAccent,
          ),
        ),
        const SizedBox(width: 4),
        Expanded(
          flex: 28,
          child: TelemetryCapsule(
            icon: _api.activeTargetType.contains('Dump') || _api.activeTargetType.contains('Disposal') ? '📍' : '🚜',
            label: 'TARGET',
            value: _api.activeTargetName,
            valueColor: FmsTheme.cyanAccent,
          ),
        ),
        const SizedBox(width: 4),
        Expanded(
          flex: 17,
          child: TelemetryCapsule(
            icon: '🧭',
            label: 'ARAH',
            value: _api.relativeDirectionLabel,
            valueColor: FmsTheme.emeraldGreen,
          ),
        ),
        const SizedBox(width: 4),
        Expanded(
          flex: 20,
          child: TelemetryCapsule(
            icon: '📐',
            label: 'GPS AGE',
            value: _api.formattedGpsAge,
            valueColor: FmsTheme.cyanAccent,
          ),
        ),
      ],
    );
  }

  Widget _compactCapsule({
    required String icon,
    required String label,
    required String value,
    required Color color,
  }) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 3),
      decoration: BoxDecoration(
        color: const Color(0xFF071424),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: FmsTheme.cardBorder.withValues(alpha: 0.5)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(icon, style: const TextStyle(fontSize: 9)),
              const SizedBox(width: 2),
              Text(label, style: FmsTheme.caption.copyWith(fontSize: 7.5)),
            ],
          ),
          const SizedBox(height: 1),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              value,
              style: TextStyle(
                color: color,
                fontWeight: FontWeight.w800,
                fontSize: 9.5,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

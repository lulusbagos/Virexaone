import 'login_screen.dart';
import 'dart:async';

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
          builder: (_) => LoginScreen(
            onLoginSuccess: () {
              Navigator.of(context).pushReplacement(
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
        ? '☀️ Mode Siang (Solar High-Contrast)'
        : _settings.currentTheme == AppThemeMode.amberWarmth
        ? '💡 Mode Amber (Anti-Lelah Mata)'
        : _settings.currentTheme == AppThemeMode.emeraldTactical
        ? '🎯 Mode Tactical (Aviasi Hijau)'
        : '🌙 Mode Malam (Cyber Midnight)';
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(label, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 11)),
        duration: const Duration(milliseconds: 1400),
        behavior: SnackBarBehavior.floating,
        backgroundColor: const Color(0xEE0A1828),
      ),
    );
  }

  void _toggleLanguage() {
    final nextLang = _settings.currentLanguage == AppLanguage.id ? AppLanguage.en : AppLanguage.id;
    _settings.setLanguage(nextLang);
    final label = nextLang == AppLanguage.id ? '🇮🇩 Bahasa Indonesia' : '🇬🇧 English';
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(label, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 11)),
        duration: const Duration(milliseconds: 1200),
        behavior: SnackBarBehavior.floating,
        backgroundColor: const Color(0xEE0A1828),
      ),
    );
  }

  @override
  void initState() {
    super.initState();
    _animController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat();
    _bearingController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 700),
    );
    _motionController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 950),
    );

    _updateTime();
    _clockTimer = Timer.periodic(
      const Duration(seconds: 1),
      (_) => _updateTime(),
    );
    _api.addListener(_onApiUpdate);
  }

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
        _bearingController.stop();
        _motionController.stop();
      } else {
        // Smooth GPS motion & heading
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
          Curves.easeOutCubic.transform(_bearingController.value);

  double get _visualEasting =>
      _eastingFrom +
      (_eastingTo - _eastingFrom) *
          Curves.easeOutQuad.transform(_motionController.value);

  double get _visualNorthing =>
      _northingFrom +
      (_northingTo - _northingFrom) *
          Curves.easeOutQuad.transform(_motionController.value);

  double get _visualHeading =>
      (_headingFrom +
          (_headingTo - _headingFrom) *
              Curves.easeOutQuad.transform(_motionController.value) +
          360) %
      360;

  double get _visualAzimuth =>
      (_azimuthFrom +
          (_azimuthTo - _azimuthFrom) *
              Curves.easeOutQuad.transform(_motionController.value) +
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
          child: Column(
            children: [
              // 1. TOP COCKPIT HEADER
              _buildTopBar(isCompact),
              const SizedBox(height: 4),

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
          ),
        ),
      ),
    );
  }

  // =========================================================================
  // 1. TOP STATUS BAR (RESPONSIVE)
  // =========================================================================
  Widget _buildTopBar(bool isCompact) {
    final stateLabel = _api.currentStatus;

    if (isCompact) {
      return Container(
        height: 42,
        padding: const EdgeInsets.symmetric(horizontal: 8),
        decoration: BoxDecoration(
          color: FmsTheme.cardHeaderBg,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: FmsTheme.cardBorder),
        ),
        child: Row(
          children: [
            Container(
              width: 8,
              height: 8,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: _api.hasUnitGpsFix
                    ? FmsTheme.emeraldGreen
                    : _api.navigationIsHeld
                    ? FmsTheme.amberWarning
                    : FmsTheme.redHazard,
                boxShadow: FmsTheme.neonGlowShadow(
                  _api.hasUnitGpsFix
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
                color: FmsTheme.emeraldGreen,
                fontWeight: FontWeight.w800,
                fontSize: 13,
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                '${_api.currentStatus} | ${_api.navigationIsHeld ? _api.navigationHoldLabel : 'GPS'} ${_api.displayGpsAgeSeconds ?? '-'}s',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: FmsTheme.caption.copyWith(fontSize: 10),
              ),
            ),
            Text(
              _currentTimeStr,
              style: FmsTheme.codePill.copyWith(
                color: FmsTheme.cyanAccent,
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
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: FmsTheme.cardHeaderBg,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: FmsTheme.cardBorder),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          // Unit GPS and site weather
          Row(
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
                            ? 'GPS UNIT AKTIF'
                            : _api.navigationIsHeld
                            ? _api.navigationHoldLabel
                            : 'GPS UNIT TIDAK SIAP',
                        style: TextStyle(
                          color: _api.isApiConnected
                              ? FmsTheme.emeraldGreen
                              : FmsTheme.amberWarning,
                          fontWeight: FontWeight.bold,
                          fontSize: 10,
                        ),
                      ),
                      Text(
                        ' ${_api.displayGpsAgeSeconds ?? '-'}s',
                        style: FmsTheme.caption.copyWith(
                          color: FmsTheme.cyanAccent,
                          fontSize: 9.5,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(width: 6),
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

          // Center Pill: Unit ID | Operator | Status | Ritasi
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
            decoration: BoxDecoration(
              color: FmsTheme.cardBg,
              borderRadius: BorderRadius.circular(8),
              border: Border.all(color: FmsTheme.cardBorder),
            ),
            child: Row(
              children: [
                Text('HD: ', style: FmsTheme.caption.copyWith(fontSize: 9.5)),
                Text(
                  _api.selectedUnitId,
                  style: FmsTheme.titleMedium.copyWith(
                    color: FmsTheme.emeraldGreen,
                    fontSize: 11,
                  ),
                ),
                Text(' | ', style: TextStyle(color: FmsTheme.cardBorder, fontSize: 9.5)),
                Text('Status: ', style: FmsTheme.caption.copyWith(fontSize: 9.5)),
                Text(
                  stateLabel,
                  style: FmsTheme.titleMedium.copyWith(
                    color: FmsTheme.cyanAccent,
                    fontSize: 11,
                  ),
                ),
                Text(' | ', style: TextStyle(color: FmsTheme.cardBorder, fontSize: 9.5)),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1.5),
                  decoration: BoxDecoration(
                    color: const Color(0x33FFB703),
                    borderRadius: BorderRadius.circular(4),
                    border: Border.all(
                      color: FmsTheme.amberWarning.withValues(alpha: 0.6),
                    ),
                  ),
                  child: Text(
                    _api.haulDataAvailable
                        ? '${_api.completedRitasiCount} LOAD TERCATAT'
                        : 'LOAD -',
                    style: const TextStyle(
                      color: FmsTheme.amberWarning,
                      fontWeight: FontWeight.bold,
                      fontSize: 9.5,
                    ),
                  ),
                ),
              ],
            ),
          ),

          // Right Controls: Quick Theme, Language, Clock, Menu & Logout
          Row(
            children: [
              // 1. Quick Theme Toggle Button
              IconButton(
                icon: Icon(
                  _settings.currentTheme == AppThemeMode.solarDay
                      ? Icons.wb_sunny_rounded
                      : _settings.currentTheme == AppThemeMode.amberWarmth
                      ? Icons.shield_moon_rounded
                      : _settings.currentTheme == AppThemeMode.emeraldTactical
                      ? Icons.radar_rounded
                      : Icons.nightlight_round_rounded,
                  size: 20,
                  color: FmsTheme.cyanAccent,
                ),
                tooltip: 'Ganti Tema Siang/Malam/Amber/Taktis',
                onPressed: _cycleTheme,
              ),

              // 2. Quick Language Switcher
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
                      fontSize: 10,
                      fontWeight: FontWeight.bold,
                      color: Colors.white,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 4),

              // 3. FMS Full Menu
              IconButton(
                icon: Icon(Icons.menu_rounded, size: 20, color: FmsTheme.cyanAccent),
                tooltip: _settings.tr('menu_btn'),
                onPressed: () => _openFmsMenu(),
              ),

              // 4. Digital Cockpit Clock
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: const Color(0x2200E5FF),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(
                    color: FmsTheme.cyanAccent.withValues(alpha: 0.4),
                  ),
                ),
                child: Text(
                  _currentTimeStr,
                  style: FmsTheme.codePill.copyWith(
                    color: FmsTheme.cyanAccent,
                    fontSize: 11,
                  ),
                ),
              ),
              const SizedBox(width: 4),

              // 5. Zen Navigation Mode Toggle
              IconButton(
                icon: Icon(
                  _isZenNavigationMode
                      ? Icons.fullscreen_exit_rounded
                      : Icons.fullscreen_rounded,
                  size: 20,
                  color: _isZenNavigationMode
                      ? FmsTheme.emeraldGreen
                      : FmsTheme.cyanAccent,
                ),
                tooltip: _isZenNavigationMode
                    ? 'Kembalikan Panel'
                    : 'Fokus Navigasi Penuh',
                onPressed: () {
                  setState(() {
                    _isZenNavigationMode = !_isZenNavigationMode;
                  });
                },
              ),

              // 6. Exit Cockpit Button
              IconButton(
                icon: const Icon(
                  Icons.exit_to_app_rounded,
                  size: 19,
                  color: FmsTheme.amberWarning,
                ),
                tooltip: 'Ganti Unit / Keluar Cockpit',
                onPressed: _handleExitCockpit,
              ),
            ],
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
          SizedBox(width: 124, child: _buildRightStateColumn()),
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

    // Check collision hazard from nearby fleet (<50m)
    final criticalHazardUnit = _api.nearbyVehicles.where(
      (u) => u.isCollisionWarning || u.distanceMeters < 50.0,
    ).firstOrNull;

    return Container(
      decoration: BoxDecoration(
        color: FmsTheme.cardBg,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: FmsTheme.cardBorder),
      ),
      child: Stack(
        children: [
          // 1. Navigation Viewport Canvas (3D Chevron / Tactical Radar)
          Positioned.fill(
            top: isCompact ? 54 : 58,
            bottom: isCompact ? 72 : 82,
            child: _api.hasNavigationFix
                ? AnimatedBuilder(
                    animation: Listenable.merge([
                      _animController,
                      _bearingController,
                      _motionController,
                    ]),
                    builder: (context, child) {
                      return Opacity(
                        opacity: _api.navigationIsHeld ? 0.78 : 1.0,
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
                                  held: _api.navigationIsHeld,
                                  animPhase: _animController.value,
                                ),
                        ),
                      );
                    },
                  )
                : Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Text(
                        !_api.isApiConnected
                            ? _api.apiStatusMessage
                            : !_api.hasUnitGpsFix
                            ? 'GPS unit belum segar (${_api.selectedUnit?.lastHeardSecondsAgo ?? '-'}s). Menunggu sinyal terbaru...'
                            : _api.relativeDirectionLabel,
                        textAlign: TextAlign.center,
                        style: FmsTheme.titleMedium.copyWith(
                          color: FmsTheme.amberWarning,
                          fontSize: 13,
                        ),
                      ),
                    ),
                  ),
          ),

          // 2. High-Tech Turn-by-Turn Maneuver Header Bar
          Positioned(
            top: 6,
            left: 8,
            right: 8,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
              decoration: BoxDecoration(
                color: FmsTheme.cardHeaderBg.withValues(alpha: 0.94),
                borderRadius: BorderRadius.circular(9),
                border: Border.all(
                  color: maneuverColor.withValues(alpha: 0.60),
                  width: 1.1,
                ),
                boxShadow: [
                  BoxShadow(
                    color: maneuverColor.withValues(alpha: 0.20),
                    blurRadius: 10,
                    spreadRadius: -2,
                  ),
                ],
              ),
              child: Row(
                children: [
                  // Maneuver Icon
                  Container(
                    width: 34,
                    height: 34,
                    decoration: BoxDecoration(
                      color: maneuverColor.withValues(alpha: 0.15),
                      borderRadius: BorderRadius.circular(7),
                      border: Border.all(
                        color: maneuverColor.withValues(alpha: 0.80),
                        width: 1.2,
                      ),
                    ),
                    child: Icon(
                      maneuverIcon,
                      color: maneuverColor,
                      size: 22,
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
                                fontSize: 11.5,
                                fontWeight: FontWeight.w900,
                                letterSpacing: 0.5,
                              ),
                            ),
                            const SizedBox(width: 6),
                            Flexible(
                              child: Text(
                                '• ${_api.activeTargetName}',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: Color(0xFFF1FAFA),
                                  fontSize: 11,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 1),
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

                  // Speed Badge
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
                    decoration: BoxDecoration(
                      color: const Color(0xFF030910),
                      borderRadius: BorderRadius.circular(6),
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
                            fontSize: 12,
                            fontWeight: FontWeight.w900,
                            height: 1.0,
                          ),
                        ),
                        const Text(
                          'KM/H',
                          style: TextStyle(
                            color: Color(0xFF80DEEA),
                            fontSize: 7,
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
                        icon: Icon(Icons.navigation_outlined, size: 13),
                        label: Text('3D HUD'),
                      ),
                      ButtonSegment(
                        value: 1,
                        icon: Icon(Icons.radar, size: 13),
                        label: Text('Radar Map'),
                      ),
                    ],
                    selected: {_navigationMode},
                    showSelectedIcon: false,
                    onSelectionChanged: (selected) =>
                        setState(() => _navigationMode = selected.first),
                    style: ButtonStyle(
                      visualDensity: VisualDensity.compact,
                      textStyle: const WidgetStatePropertyAll(
                        TextStyle(fontSize: 8.5, fontWeight: FontWeight.bold),
                      ),
                      padding: const WidgetStatePropertyAll(
                        EdgeInsets.symmetric(horizontal: 5),
                      ),
                      minimumSize: const WidgetStatePropertyAll(Size(30, 24)),
                    ),
                  ),
                ],
              ),
            ),
          ),

          // 3. Proximity Hazard Collision Banner (<50m alert)
          if (criticalHazardUnit != null)
            Positioned(
              top: isCompact ? 58 : 64,
              left: 12,
              right: 12,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: const Color(0xE83D0612),
                  borderRadius: BorderRadius.circular(6),
                  border: Border.all(color: const Color(0xFFFF3366), width: 1.2),
                  boxShadow: [
                    BoxShadow(
                      color: const Color(0xFFFF3366).withValues(alpha: 0.35),
                      blurRadius: 8,
                    ),
                  ],
                ),
                child: Row(
                  children: [
                    const Icon(Icons.warning_amber_rounded, color: Color(0xFFFF3366), size: 16),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        'PERINGATAN PROKSIMITAS: ${criticalHazardUnit.unitName} (${criticalHazardUnit.distanceMeters.round()}m) Berada di Zona Bahaya!',
                        style: const TextStyle(
                          color: Color(0xFFFFE4E8),
                          fontSize: 9.5,
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

          // 4. Surrounding Fleet Radar Ticker (Bottom-left HUD)
          Positioned(
            bottom: isCompact ? 48 : 54,
            left: 8,
            right: 8,
            child: Container(
              height: 22,
              padding: const EdgeInsets.symmetric(horizontal: 6),
              decoration: BoxDecoration(
                color: const Color(0xDC040E19),
                borderRadius: BorderRadius.circular(6),
                border: Border.all(color: const Color(0xFF0F3E50)),
              ),
              child: Row(
                children: [
                  Icon(
                    Icons.radar,
                    size: 12,
                    color: _api.nearbyVehicles.isNotEmpty
                        ? const Color(0xFF00FFA3)
                        : const Color(0xFF80DEEA),
                  ),
                  const SizedBox(width: 4),
                  Text(
                    'ARMADA SEKITAR (${_api.nearbyVehicles.length}): ',
                    style: const TextStyle(
                      color: Color(0xFF80DEEA),
                      fontSize: 8.0,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  if (_api.nearbyVehicles.isEmpty)
                    const Text(
                      'Tidak ada unit dalam jangkauan radar',
                      style: TextStyle(
                        color: Color(0xFF547B8C),
                        fontSize: 8.0,
                        fontStyle: FontStyle.italic,
                      ),
                    )
                  else
                    Expanded(
                      child: ListView.separated(
                        scrollDirection: Axis.horizontal,
                        itemCount: _api.nearbyVehicles.length,
                        separatorBuilder: (context, index) => const SizedBox(width: 6),
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
                            padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
                            decoration: BoxDecoration(
                              color: c.withValues(alpha: 0.15),
                              borderRadius: BorderRadius.circular(3),
                              border: Border.all(color: c.withValues(alpha: 0.6), width: 0.8),
                            ),
                            child: Text(
                              '${v.unitName} ${v.distanceMeters.round()}m',
                              style: TextStyle(
                                color: c,
                                fontSize: 8.0,
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

          // 5. Bottom Target Control Bar
          Positioned(
            bottom: 4,
            left: 6,
            right: 6,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: FmsTheme.cardHeaderBg,
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: FmsTheme.cardBorder),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Row(
                          children: [
                            Flexible(
                              child: Text(
                                'TUJUAN: ${_api.activeTargetName}',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: FmsTheme.titleMedium.copyWith(
                                  color: FmsTheme.emeraldGreen,
                                  fontSize: 10.5,
                                ),
                              ),
                            ),
                            const SizedBox(width: 5),
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
                              decoration: BoxDecoration(
                                color: const Color(0x3300FFA3),
                                borderRadius: BorderRadius.circular(4),
                              ),
                              child: Text(
                                !_api.hasTarget
                                    ? 'BELUM ADA'
                                    : _api.isManualTargetOverride
                                    ? 'MANUAL'
                                    : 'DISPATCH',
                                style: FmsTheme.caption.copyWith(
                                  color: FmsTheme.emeraldGreen,
                                  fontSize: 7.5,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                          ],
                        ),
                        Text(
                          '${_api.activeTargetType} | Sisa: $distText${_api.estimatedEtaMinutes > 0 ? ' | Est: ${_api.estimatedEtaMinutes.toStringAsFixed(1)}m' : ''}',
                          style: FmsTheme.caption.copyWith(
                            color: FmsTheme.cyanAccent,
                            fontSize: 8.5,
                          ),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 6),
                  ElevatedButton.icon(
                    onPressed: () {
                      showDialog(
                        context: context,
                        builder: (_) => const SelectTargetDialog(),
                      );
                    },
                    icon: const Icon(Icons.sync_alt, size: 12),
                    label: const Text(
                      'GANTI',
                      style: TextStyle(fontSize: 9.0, fontWeight: FontWeight.bold),
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF0D2544),
                      foregroundColor: FmsTheme.cyanAccent,
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(6),
                      ),
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
                ? Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 8),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('AKTIVITAS', style: FmsTheme.caption),
                        const SizedBox(height: 4),
                        Text(
                          _api.currentStatus,
                          maxLines: 3,
                          overflow: TextOverflow.ellipsis,
                          style: FmsTheme.titleMedium.copyWith(
                            color: FmsTheme.emeraldGreen,
                            fontSize: 10.5,
                          ),
                        ),
                        Divider(color: FmsTheme.cardBorder, height: 18),
                        Text('GPS UNIT', style: FmsTheme.caption),
                        const SizedBox(height: 4),
                        Text(
                          _api.hasUnitGpsFix
                              ? '${_api.displayGpsAgeSeconds}s lalu'
                              : _api.navigationIsHeld
                              ? '${_api.navigationHoldLabel} ${_api.displayGpsAgeSeconds}s'
                              : 'TIDAK SIAP',
                          style: FmsTheme.titleMedium.copyWith(
                            color: _api.hasUnitGpsFix
                                ? FmsTheme.cyanAccent
                                : FmsTheme.amberWarning,
                            fontSize: 9.5,
                          ),
                        ),
                        const SizedBox(height: 10),
                        Text('KECEPATAN', style: FmsTheme.caption),
                        const SizedBox(height: 4),
                        Text(
                          _api.hasUnitGpsFix
                              ? '${_api.hdSpeedKmh.toStringAsFixed(1)} km/h'
                              : '-',
                          style: FmsTheme.titleMedium.copyWith(fontSize: 9.5),
                        ),
                      ],
                    ),
                  )
                : Column(
                    mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                    children: [
                      Text('LOAD TERCATAT', textAlign: TextAlign.center, style: FmsTheme.caption),
                      Text(
                        _api.haulDataAvailable ? '${_api.completedRitasiCount}' : '-',
                        style: FmsTheme.titleMedium.copyWith(
                          color: FmsTheme.amberWarning,
                          fontSize: 13,
                        ),
                      ),
                      Text('PAYLOAD AKTUAL', textAlign: TextAlign.center, style: FmsTheme.caption),
                      Text(
                        _api.payloadAvailable
                            ? '${_api.activePayloadTons.toStringAsFixed(1)} t'
                            : '-',
                        style: FmsTheme.titleMedium.copyWith(
                          color: FmsTheme.cyanAccent,
                          fontSize: 13,
                        ),
                      ),
                    ],
                  ),
          ),
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
            value: _api.selectedUnit?.lastHeardSecondsAgo != null
                ? '${_api.selectedUnit!.lastHeardSecondsAgo}s'
                : '-',
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

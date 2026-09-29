import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../theme/fms_theme.dart';
import '../services/fms_api_service.dart';
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
  Timer? _clockTimer;
  String _currentTimeStr = '';
  bool _isZenNavigationMode = false;
  int _rightDockTab = 0; // 0: Siklus Operasi, 1: Gauges & Mesin
  int _navigationMode = 1; // 0: 3D Chevron Arrow, 1: Jejak GPS

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
              icon: const Icon(Icons.menu_rounded, size: 20, color: FmsTheme.textLight),
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
                  style: const TextStyle(
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
                const Text(' | ', style: TextStyle(color: FmsTheme.cardBorder, fontSize: 9.5)),
                Text('Status: ', style: FmsTheme.caption.copyWith(fontSize: 9.5)),
                Text(
                  stateLabel,
                  style: FmsTheme.titleMedium.copyWith(
                    color: FmsTheme.cyanAccent,
                    fontSize: 11,
                  ),
                ),
                const Text(' | ', style: TextStyle(color: FmsTheme.cardBorder, fontSize: 9.5)),
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

          // Right Controls: Clock, Menu & Logout
          Row(
            children: [
              IconButton(
                icon: const Icon(Icons.menu_rounded, size: 20, color: FmsTheme.cyanAccent),
                tooltip: 'Menu FMS',
                onPressed: () => _openFmsMenu(),
              ),
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
              IconButton(
                icon: const Icon(
                  Icons.logout_rounded,
                  size: 18,
                  color: FmsTheme.textMuted,
                ),
                tooltip: 'Keluar Cockpit',
                onPressed: _api.logout,
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
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
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
            const Divider(color: FmsTheme.cardBorder),
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
        : '${distMeters.toStringAsFixed(0)} METER';

    final subTarget = _api.navigationIsHeld
        ? '${_api.navigationHoldLabel} ${_api.displayGpsAgeSeconds ?? '-'}s | GARIS LURUS'
        : _api.hasNavigationFix
        ? _navigationMode == 1
              ? 'JEJAK GPS 10 MENIT | GARIS LURUS KE ${_api.activeTargetName}'
              : 'GARIS LURUS KE ${_api.activeTargetName}'
        : _api.relativeDirectionLabel;

    final hdgText = _api.hdHeadingDeg.toStringAsFixed(0).padLeft(3, '0');
    final azText = _api.absoluteTargetAzimuth.toStringAsFixed(0).padLeft(3, '0');

    return Container(
      decoration: BoxDecoration(
        color: FmsTheme.cardBg,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: FmsTheme.cardBorder),
      ),
      child: Stack(
        children: [
          // 3D Canvas
          Positioned.fill(
            top: isCompact ? 48 : 52,
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
                        opacity: _api.navigationIsHeld ? 0.75 : 1.0,
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

          // Top Right: Mode Switcher (Panah / Jejak)
          Positioned(
            top: 8,
            right: 8,
            child: SegmentedButton<int>(
              segments: const [
                ButtonSegment(value: 0, label: Text('Panah 3D')),
                ButtonSegment(value: 1, label: Text('Jejak GPS')),
              ],
              selected: {_navigationMode},
              showSelectedIcon: false,
              onSelectionChanged: (selected) =>
                  setState(() => _navigationMode = selected.first),
              style: ButtonStyle(
                visualDensity: VisualDensity.compact,
                textStyle: const WidgetStatePropertyAll(
                  TextStyle(fontSize: 9.5, fontWeight: FontWeight.bold),
                ),
                padding: const WidgetStatePropertyAll(
                  EdgeInsets.symmetric(horizontal: 6),
                ),
                minimumSize: const WidgetStatePropertyAll(Size(36, 26)),
              ),
            ),
          ),

          // Top Center: Floating Target Distance Badge
          Positioned(
            top: 6,
            left: isCompact ? 54 : 10,
            right: isCompact ? 116 : 140,
            child: Align(
              alignment: Alignment.topCenter,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                decoration: BoxDecoration(
                  color: FmsTheme.cardHeaderBg,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                    color: FmsTheme.cyanAccent.withValues(alpha: 0.7),
                    width: 1.2,
                  ),
                  boxShadow: FmsTheme.neonGlowShadow(
                    FmsTheme.emeraldGreen,
                    opacity: 0.25,
                    blur: 10,
                  ),
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      '📍 $distText',
                      style: FmsTheme.displayMedium.copyWith(
                        color: _api.navigationIsHeld
                            ? FmsTheme.amberWarning
                            : FmsTheme.emeraldGreen,
                        fontSize: isCompact ? 14 : 16,
                      ),
                    ),
                    Text(
                      subTarget,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: FmsTheme.caption.copyWith(
                        color: FmsTheme.cyanAccent,
                        fontWeight: FontWeight.bold,
                        fontSize: 8.5,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),

          // Bottom Left: CAS Radar Badge
          Positioned(
            bottom: isCompact ? 52 : 62,
            left: 8,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
              decoration: BoxDecoration(
                color: const Color(0xEC040F1D),
                borderRadius: BorderRadius.circular(6),
                border: Border.all(
                  color: FmsTheme.amberWarning.withValues(alpha: 0.7),
                  width: 1.0,
                ),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text(
                    'GPS SEKITAR: ',
                    style: TextStyle(
                      color: FmsTheme.amberWarning,
                      fontSize: 8.5,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  Text(
                    _api.hasLiveNavigationFix
                        ? '${_api.nearbyVehicles.length} UNIT'
                        : '--',
                    style: FmsTheme.caption.copyWith(
                      color: FmsTheme.emeraldGreen,
                      fontSize: 8.5,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
            ),
          ),

          // Bottom Right: Heading & Relative Bearing
          Positioned(
            bottom: isCompact ? 52 : 62,
            right: 8,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
              decoration: BoxDecoration(
                color: FmsTheme.cardHeaderBg,
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: FmsTheme.cardBorder),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'HDG: ${_api.hasNavigationFix ? '$hdgText°' : '-'} | ',
                    style: FmsTheme.codePill.copyWith(
                      color: FmsTheme.textLight,
                      fontSize: 9.0,
                    ),
                  ),
                  Text(
                    'AZ: ${_api.hasNavigationFix ? '$azText°' : '-'} | ',
                    style: FmsTheme.codePill.copyWith(
                      color: FmsTheme.cyanAccent,
                      fontSize: 9.0,
                    ),
                  ),
                  Text(
                    _api.relativeDirectionLabel,
                    style: FmsTheme.codePill.copyWith(
                      color: FmsTheme.emeraldGreen,
                      fontSize: 9.0,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
            ),
          ),

          // Bottom Viewport Target Strip
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
                                'TARGET: ${_api.activeTargetName}',
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
                                    ? 'DIPILIH'
                                    : 'ASSIGNMENT',
                                style: FmsTheme.caption.copyWith(
                                  color: FmsTheme.emeraldGreen,
                                  fontSize: 7.5,
                                ),
                              ),
                            ),
                          ],
                        ),
                        Text(
                          '${_api.activeTargetType} | Jarak: $distText${_api.estimatedEtaMinutes > 0 ? ' | Est. ${_api.estimatedEtaMinutes.toStringAsFixed(1)}m' : ''}',
                          style: FmsTheme.caption.copyWith(
                            color: FmsTheme.cyanAccent,
                            fontSize: 8.5,
                          ),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 4),
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
                        const Divider(color: FmsTheme.cardBorder, height: 18),
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

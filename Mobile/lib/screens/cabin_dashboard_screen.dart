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
  double _bearingFrom = 0;
  double _bearingTo = 0;
  bool _bearingReady = false;
  String _bearingUnit = '';
  String _bearingTarget = '';
  final FmsApiService _api = FmsApiService();
  Timer? _clockTimer;
  String _currentTimeStr = "";
  bool _isZenNavigationMode = false;
  int _rightDockTab = 0; // 0: Siklus Operasi, 1: Gauges & Mesin
  int _navigationMode = 1;

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
      _bearingController.stop();
    } else {
      final next = _api.relativeBearingDegrees;
      if (!_bearingReady ||
          _bearingUnit != _api.selectedUnitId ||
          _bearingTarget != _api.activeTargetName) {
        _bearingFrom = next;
        _bearingTo = next;
        _bearingReady = true;
        _bearingUnit = _api.selectedUnitId;
        _bearingTarget = _api.activeTargetName;
        _bearingController.stop();
      } else {
        final current = _visualBearing;
        final turn = (next - current + 540) % 360 - 180;
        if (turn.abs() > 0.5) {
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
    _api.removeListener(_onApiUpdate);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: FmsTheme.bgDark,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 6.0, vertical: 4.0),
          child: Column(
            children: [
              // 1. TOP APP BAR (COCKPIT HEADER)
              _buildTopBar(),
              const SizedBox(height: 4),

              // 2. MAIN COCKPIT VIEWPORT
              Expanded(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // LEFT COLUMN: SLIM ERGONOMIC THUMB DOCK (Visible if not Zen Mode)
                    if (!_isZenNavigationMode) ...[
                      SizedBox(width: 66, child: _buildLeftActionColumn()),
                      const SizedBox(width: 5),
                    ],

                    // CENTER COLUMN: 3D ARROW HUD & RADAR (MAX EXPANDED)
                    Expanded(child: _buildCenterNavigationColumn()),

                    // RIGHT COLUMN: SLIM CYCLE & TELEMETRY DOCK (Visible if not Zen Mode)
                    if (!_isZenNavigationMode) ...[
                      const SizedBox(width: 5),
                      SizedBox(width: 120, child: _buildRightStateColumn()),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 4),

              // 3. BOTTOM STATUS CAPSULES
              _buildBottomTelemetryBar(),
            ],
          ),
        ),
      ),
    );
  }

  // =========================================================================
  // 1. TOP STATUS BAR
  // =========================================================================
  Widget _buildTopBar() {
    final stateLabel = _api.currentStatus;
    if (MediaQuery.sizeOf(context).width < 1100) {
      return Container(
        height: 40,
        padding: const EdgeInsets.symmetric(horizontal: 8),
        decoration: BoxDecoration(
          color: FmsTheme.cardHeaderBg,
          border: Border.all(color: FmsTheme.cardBorder),
        ),
        child: Row(
          children: [
            Icon(
              _api.hasUnitGpsFix
                  ? Icons.gps_fixed
                  : _api.navigationIsHeld
                  ? Icons.history
                  : Icons.gps_off,
              color: _api.hasUnitGpsFix
                  ? FmsTheme.emeraldGreen
                  : FmsTheme.amberWarning,
              size: 17,
            ),
            const SizedBox(width: 7),
            Text(
              _api.selectedUnitId,
              style: FmsTheme.titleMedium.copyWith(
                color: FmsTheme.emeraldGreen,
                fontSize: 12,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                '${_api.currentStatus}  |  ${_api.navigationIsHeld ? _api.navigationHoldLabel : 'GPS'} ${_api.displayGpsAgeSeconds ?? '-'} s',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: FmsTheme.caption.copyWith(fontSize: 10),
              ),
            ),
            Text(
              _currentTimeStr,
              style: FmsTheme.caption.copyWith(color: FmsTheme.cyanAccent),
            ),
            IconButton(
              tooltip: 'Menu FMS',
              icon: const Icon(Icons.menu, size: 19),
              onPressed: _openFmsMenu,
            ),
            IconButton(
              tooltip: 'Pengaturan API',
              icon: const Icon(Icons.settings, size: 18),
              onPressed: () => showDialog(
                context: context,
                builder: (_) => const ServerSettingsDialog(),
              ),
            ),
            IconButton(
              tooltip: _isZenNavigationMode
                  ? 'Tampilkan panel'
                  : 'Fokus navigasi',
              icon: Icon(
                _isZenNavigationMode ? Icons.fullscreen_exit : Icons.fullscreen,
                size: 18,
              ),
              onPressed: () =>
                  setState(() => _isZenNavigationMode = !_isZenNavigationMode),
            ),
            IconButton(
              tooltip: 'Pilih unit lain',
              icon: const Icon(Icons.logout, size: 18),
              onPressed: _api.logout,
            ),
          ],
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: FmsTheme.cardHeaderBg,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: FmsTheme.cardBorder),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          // Unit GPS and site weather.
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
                  padding: const EdgeInsets.symmetric(
                    horizontal: 7,
                    vertical: 3,
                  ),
                  decoration: BoxDecoration(
                    color: _api.isApiConnected
                        ? const Color(0x2200FFA3)
                        : const Color(0x33FFB703),
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(
                      color: _api.isApiConnected
                          ? FmsTheme.emeraldGreen.withValues(alpha: 0.5)
                          : FmsTheme.amberWarning,
                    ),
                  ),
                  child: Row(
                    children: [
                      Text(
                        _api.hasUnitGpsFix
                            ? "GPS UNIT AKTIF"
                            : _api.navigationIsHeld
                            ? _api.navigationHoldLabel
                            : "GPS UNIT TIDAK SIAP",
                        style: TextStyle(
                          color: _api.isApiConnected
                              ? FmsTheme.emeraldGreen
                              : FmsTheme.amberWarning,
                          fontWeight: FontWeight.bold,
                          fontSize: 10,
                        ),
                      ),
                      Text(
                        " ${_api.displayGpsAgeSeconds ?? '-'}s",
                        style: FmsTheme.caption.copyWith(
                          color: FmsTheme.cyanAccent,
                          fontSize: 9.5,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(width: 4),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 3),
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
                    fontSize: 9.0,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ],
          ),

          // Center Pill: Unit ID | Operator | Status | Ritasi
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2.5),
            decoration: BoxDecoration(
              color: FmsTheme.cardBg,
              borderRadius: BorderRadius.circular(8),
              border: Border.all(color: FmsTheme.cardBorder),
            ),
            child: Row(
              children: [
                Text("HD: ", style: FmsTheme.caption.copyWith(fontSize: 9.5)),
                Text(
                  _api.selectedUnitId,
                  style: FmsTheme.titleMedium.copyWith(
                    color: FmsTheme.emeraldGreen,
                    fontSize: 10.5,
                  ),
                ),
                const Text(
                  " | ",
                  style: TextStyle(color: FmsTheme.cardBorder, fontSize: 9.5),
                ),
                Text("Opr: ", style: FmsTheme.caption.copyWith(fontSize: 9.5)),
                Text(
                  _api.operatorName,
                  style: FmsTheme.titleMedium.copyWith(fontSize: 10.5),
                ),
                const Text(
                  " | ",
                  style: TextStyle(color: FmsTheme.cardBorder, fontSize: 9.5),
                ),
                Text(
                  "Status: ",
                  style: FmsTheme.caption.copyWith(fontSize: 9.5),
                ),
                Text(
                  stateLabel,
                  style: FmsTheme.titleMedium.copyWith(
                    color: FmsTheme.cyanAccent,
                    fontSize: 10.5,
                  ),
                ),
                const Text(
                  " | ",
                  style: TextStyle(color: FmsTheme.cardBorder, fontSize: 9.5),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 4,
                    vertical: 1,
                  ),
                  decoration: BoxDecoration(
                    color: const Color(0x33FFB703),
                    borderRadius: BorderRadius.circular(4),
                    border: Border.all(
                      color: FmsTheme.amberWarning.withValues(alpha: 0.6),
                    ),
                  ),
                  child: Row(
                    children: [
                      const Text("⚡ ", style: TextStyle(fontSize: 8.5)),
                      Text(
                        _api.haulDataAvailable
                            ? "${_api.completedRitasiCount} LOAD TERCATAT"
                            : "LOAD -",
                        style: const TextStyle(
                          color: FmsTheme.amberWarning,
                          fontWeight: FontWeight.bold,
                          fontSize: 9.5,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),

          // Right Pill: Clock, Zen Fullscreen Toggle & Logout
          Row(
            children: [
              IconButton(
                icon: const Icon(
                  Icons.menu_rounded,
                  size: 20,
                  color: FmsTheme.cyanAccent,
                ),
                tooltip: 'Menu FMS',
                onPressed: _openFmsMenu,
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
                decoration: BoxDecoration(
                  color: const Color(0x2200E5FF),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(
                    color: FmsTheme.cyanAccent.withValues(alpha: 0.4),
                  ),
                ),
                child: Row(
                  children: [
                    Text(
                      "$_currentTimeStr ",
                      style: FmsTheme.titleMedium.copyWith(
                        color: FmsTheme.cyanAccent,
                        fontSize: 10.5,
                      ),
                    ),
                  ],
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
                    ? "Kembalikan Menu Samping"
                    : "Fokus Navigasi Penuh",
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(minWidth: 26, minHeight: 26),
                onPressed: () {
                  setState(() {
                    _isZenNavigationMode = !_isZenNavigationMode;
                  });
                },
              ),
              IconButton(
                icon: const Icon(
                  Icons.logout_rounded,
                  size: 17,
                  color: FmsTheme.textMuted,
                ),
                tooltip: "Keluar Cockpit",
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(minWidth: 26, minHeight: 26),
                onPressed: () {
                  _api.logout();
                },
              ),
            ],
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
  // 2. COLUMN 1: SLIM ERGONOMIC THUMB DOCK (LEFT)
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
          // Only actions backed by read-only API data are available here.
          Expanded(
            child: _buildThumbActionButton(
              icon: Icons.sync,
              label: "SYNC",
              borderColor: FmsTheme.cyanAccent,
              bgColor: const Color(0xE0061A36),
              onTap: _api.syncFromBackend,
            ),
          ),
          const SizedBox(height: 3),
          Expanded(
            child: _buildThumbActionButton(
              icon: Icons.my_location,
              label: "TARGET",
              borderColor: FmsTheme.emeraldGreen,
              bgColor: const Color(0xE0062816),
              onTap: () => showDialog(
                context: context,
                builder: (_) => const SelectTargetDialog(),
              ),
            ),
          ),
          const SizedBox(height: 3),
          Expanded(
            child: _buildThumbActionButton(
              icon: Icons.chat_bubble_outline,
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
    int badgeCount = 0,
    bool isSelected = false,
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
            border: Border.all(
              color: borderColor,
              width: isSelected ? 1.8 : 1.0,
            ),
            boxShadow: isSelected
                ? FmsTheme.neonGlowShadow(borderColor, opacity: 0.4)
                : null,
          ),
          child: Stack(
            alignment: Alignment.center,
            children: [
              Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(icon, size: 18, color: borderColor),
                  const SizedBox(height: 1),
                  Text(
                    label,
                    style: TextStyle(
                      color: borderColor,
                      fontWeight: FontWeight.bold,
                      fontSize: 8.5,
                      letterSpacing: 0.2,
                    ),
                    textAlign: TextAlign.center,
                    maxLines: 1,
                  ),
                ],
              ),
              if (badgeCount > 0)
                Positioned(
                  top: 2,
                  right: 2,
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 4,
                      vertical: 1,
                    ),
                    decoration: BoxDecoration(
                      color: FmsTheme.redHazard,
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Text(
                      "$badgeCount",
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 8,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }

  // =========================================================================
  // 3. COLUMN 2: CENTER VIEWPORT (3D CHEVRON ARROW & EXA GUIDANCE)
  // =========================================================================
  Widget _buildCenterNavigationColumn() {
    final distMeters = _api.distanceToTargetMeters;
    final distText = !_api.hasNavigationFix
        ? '-'
        : distMeters >= 1000
        ? "${(distMeters / 1000).toStringAsFixed(2)} KM"
        : "${distMeters.toStringAsFixed(0)} METER";

    final subTarget = _api.navigationIsHeld
        ? "${_api.navigationHoldLabel} ${_api.displayGpsAgeSeconds ?? '-'}s | GARIS LURUS"
        : _api.hasNavigationFix
        ? _navigationMode == 1
              ? "JEJAK GPS 10 MENIT | GARIS LURUS KE ${_api.activeTargetName}"
              : "GARIS LURUS KE ${_api.activeTargetName}"
        : _api.relativeDirectionLabel;

    final hdgText = _api.hdHeadingDeg.toStringAsFixed(0).padLeft(3, '0');
    final azText = _api.absoluteTargetAzimuth
        .toStringAsFixed(0)
        .padLeft(3, '0');

    return Container(
      decoration: BoxDecoration(
        color: FmsTheme.cardBg,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: FmsTheme.cardBorder),
      ),
      child: Stack(
        children: [
          // The arrow is only visible when both source and target positions are fresh.
          Positioned.fill(
            top: 48,
            bottom: 88,
            child: _api.hasNavigationFix
                ? AnimatedBuilder(
                    animation: Listenable.merge([
                      _animController,
                      _bearingController,
                    ]),
                    builder: (context, child) {
                      return Opacity(
                        opacity: _api.navigationIsHeld ? 0.72 : 1,
                        child: CustomPaint(
                          painter: _navigationMode == 0
                              ? Nav3dArrowPainter(
                                  relativeBearing: _bearingReady
                                      ? _visualBearing
                                      : _api.relativeBearingDegrees,
                                  absoluteHeading: _api.hdHeadingDeg,
                                  targetAzimuth: _api.absoluteTargetAzimuth,
                                  animPhase: _animController.value,
                                  nearbyVehicles: _api.nearbyVehicles,
                                )
                              : GpsTrackPainter(
                                  track: _api.gpsTrack,
                                  nearbyVehicles: _api.nearbyVehicles,
                                  easting: _api.hdEasting,
                                  northing: _api.hdNorthing,
                                  heading: _api.hdHeadingDeg,
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
                            ? 'GPS unit belum segar (${_api.selectedUnit?.lastHeardSecondsAgo ?? '-'}s). Menunggu titik terbaru.'
                            : _api.relativeDirectionLabel,
                        textAlign: TextAlign.center,
                        style: FmsTheme.titleMedium.copyWith(
                          color: FmsTheme.amberWarning,
                        ),
                      ),
                    ),
                  ),
          ),

          Positioned(
            top: 7,
            right: 8,
            child: SegmentedButton<int>(
              segments: const [
                ButtonSegment(value: 0, label: Text('Panah')),
                ButtonSegment(value: 1, label: Text('Jejak')),
              ],
              selected: {_navigationMode},
              showSelectedIcon: false,
              onSelectionChanged: (selected) =>
                  setState(() => _navigationMode = selected.first),
              style: ButtonStyle(
                visualDensity: VisualDensity.compact,
                textStyle: const WidgetStatePropertyAll(
                  TextStyle(fontSize: 10),
                ),
                padding: const WidgetStatePropertyAll(
                  EdgeInsets.symmetric(horizontal: 6),
                ),
                minimumSize: const WidgetStatePropertyAll(Size(40, 28)),
              ),
            ),
          ),

          // Bottom-Left: CAS Proximity Radar Status
          Positioned(
            bottom: 72,
            left: 8,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
              decoration: BoxDecoration(
                color: const Color(0xE5040F1D),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(
                  color: FmsTheme.amberWarning.withValues(alpha: 0.65),
                  width: 1.0,
                ),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text(
                    "GPS SEKITAR: ",
                    style: TextStyle(
                      color: FmsTheme.amberWarning,
                      fontSize: 8.5,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  Text(
                    _api.hasLiveNavigationFix
                        ? "${_api.nearbyVehicles.length} UNIT"
                        : "--",
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

          // 2. Floating Distance Badge (Top Center)
          Positioned(
            top: 6,
            left: 8,
            right: 128,
            child: Align(
              alignment: Alignment.topCenter,
              child: Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 16,
                  vertical: 5,
                ),
                decoration: BoxDecoration(
                  color: FmsTheme.cardHeaderBg,
                  borderRadius: BorderRadius.circular(14),
                  border: Border.all(
                    color: FmsTheme.cyanAccent.withValues(alpha: 0.6),
                    width: 1.2,
                  ),
                  boxShadow: FmsTheme.neonGlowShadow(
                    FmsTheme.emeraldGreen,
                    opacity: 0.25,
                  ),
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      "📍 $distText",
                      style: FmsTheme.displayMedium.copyWith(
                        color: _api.navigationIsHeld
                            ? FmsTheme.amberWarning
                            : FmsTheme.emeraldGreen,
                        fontSize: 16,
                      ),
                    ),
                    Text(
                      subTarget,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: FmsTheme.caption.copyWith(
                        color: FmsTheme.cyanAccent,
                        fontWeight: FontWeight.bold,
                        fontSize: 9.5,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),

          // 3. Direction Pill: Heading & Relative Bearing to Target
          Positioned(
            bottom: 72,
            right: 8,
            child: Center(
              child: Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 8,
                  vertical: 2.5,
                ),
                decoration: BoxDecoration(
                  color: FmsTheme.cardHeaderBg,
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: FmsTheme.cardBorder),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      "HDG: ${_api.hasNavigationFix ? '$hdgText°' : '-'} | ",
                      style: FmsTheme.codePill.copyWith(
                        color: FmsTheme.textLight,
                        fontSize: 9.5,
                      ),
                    ),
                    Text(
                      "AZ: ${_api.hasNavigationFix ? '$azText°' : '-'} | ",
                      style: FmsTheme.codePill.copyWith(
                        color: FmsTheme.cyanAccent,
                        fontSize: 9.5,
                      ),
                    ),
                    Text(
                      _api.relativeDirectionLabel,
                      style: FmsTheme.codePill.copyWith(
                        color: FmsTheme.emeraldGreen,
                        fontSize: 9.5,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),

          // 4. Target Summary & Quick Target Switcher (Bottom Bar of Viewport)
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
                                "TARGET: ${_api.activeTargetName}",
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
                              padding: const EdgeInsets.symmetric(
                                horizontal: 4,
                                vertical: 1,
                              ),
                              decoration: BoxDecoration(
                                color: const Color(0x3300FFA3),
                                borderRadius: BorderRadius.circular(4),
                              ),
                              child: Text(
                                !_api.hasTarget
                                    ? "BELUM ADA"
                                    : _api.isManualTargetOverride
                                    ? "DIPILIH"
                                    : "ASSIGNMENT",
                                style: FmsTheme.caption.copyWith(
                                  color: FmsTheme.emeraldGreen,
                                  fontSize: 7.5,
                                ),
                              ),
                            ),
                          ],
                        ),
                        Text(
                          "${_api.activeTargetType} | Jarak lurus: $distText${_api.estimatedEtaMinutes > 0 ? ' | Est. ${_api.estimatedEtaMinutes.toStringAsFixed(1)} menit' : ''}",
                          style: FmsTheme.caption.copyWith(
                            color: FmsTheme.cyanAccent,
                            fontSize: 9.0,
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
                    icon: const Icon(Icons.sync_alt, size: 11),
                    label: const Text(
                      "GANTI",
                      style: TextStyle(
                        fontSize: 9.0,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF0D2544),
                      foregroundColor: FmsTheme.cyanAccent,
                      padding: const EdgeInsets.symmetric(
                        horizontal: 7,
                        vertical: 3,
                      ),
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
  // 4. COLUMN 3: SLIM CYCLE & TELEMETRY DOCK (RIGHT)
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
          // 2-TAB SWITCHER (SIKLUS vs MESIN)
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
                        "STATUS",
                        style: TextStyle(
                          color: _rightDockTab == 0
                              ? Colors.white
                              : FmsTheme.textMuted,
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
                        "HAUL",
                        style: TextStyle(
                          color: _rightDockTab == 1
                              ? Colors.white
                              : FmsTheme.textMuted,
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

          // TAB CONTENT
          Expanded(
            child: _rightDockTab == 0
                ? Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 5,
                      vertical: 10,
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('AKTIVITAS', style: FmsTheme.caption),
                        const SizedBox(height: 5),
                        Text(
                          _api.currentStatus,
                          maxLines: 3,
                          overflow: TextOverflow.ellipsis,
                          style: FmsTheme.titleMedium.copyWith(
                            color: FmsTheme.emeraldGreen,
                            fontSize: 11,
                          ),
                        ),
                        const Divider(color: FmsTheme.cardBorder, height: 22),
                        Text('GPS UNIT', style: FmsTheme.caption),
                        const SizedBox(height: 4),
                        Text(
                          _api.hasUnitGpsFix
                              ? '${_api.displayGpsAgeSeconds} s lalu'
                              : _api.navigationIsHeld
                              ? '${_api.navigationHoldLabel} ${_api.displayGpsAgeSeconds} s'
                              : 'TIDAK SIAP',
                          style: FmsTheme.titleMedium.copyWith(
                            color: _api.hasUnitGpsFix
                                ? FmsTheme.cyanAccent
                                : FmsTheme.amberWarning,
                            fontSize: 10,
                          ),
                        ),
                        const SizedBox(height: 14),
                        Text('KECEPATAN', style: FmsTheme.caption),
                        const SizedBox(height: 4),
                        Text(
                          _api.hasUnitGpsFix
                              ? '${_api.hdSpeedKmh.toStringAsFixed(1)} km/jam'
                              : '-',
                          style: FmsTheme.titleMedium.copyWith(fontSize: 10),
                        ),
                      ],
                    ),
                  )
                : Column(
                    mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                    children: [
                      Text(
                        'LOAD TERCATAT',
                        textAlign: TextAlign.center,
                        style: FmsTheme.caption,
                      ),
                      Text(
                        _api.haulDataAvailable
                            ? '${_api.completedRitasiCount}'
                            : '-',
                        style: FmsTheme.titleMedium.copyWith(
                          color: FmsTheme.amberWarning,
                        ),
                      ),
                      Text(
                        'PAYLOAD AKTUAL',
                        textAlign: TextAlign.center,
                        style: FmsTheme.caption,
                      ),
                      Text(
                        _api.payloadAvailable
                            ? '${_api.activePayloadTons.toStringAsFixed(1)} t'
                            : '-',
                        style: FmsTheme.titleMedium.copyWith(
                          color: FmsTheme.cyanAccent,
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
  // 5. BOTTOM STATUS CAPSULES BAR
  // =========================================================================
  Widget _buildBottomTelemetryBar() {
    return Row(
      children: [
        // Speed reported by the selected unit's GPS feed.
        Expanded(
          flex: 20,
          child: TelemetryCapsule(
            icon: "⚡",
            label: "SPEED",
            value: _api.hasUnitGpsFix
                ? "${_api.hdSpeedKmh.toStringAsFixed(0)} KM/H"
                : '-',
            valueColor: FmsTheme.emeraldGreen,
          ),
        ),
        const SizedBox(width: 4),

        // Recorded loads are not completed trips.
        Expanded(
          flex: 16,
          child: TelemetryCapsule(
            icon: "🏆",
            label: "LOAD",
            value: _api.haulDataAvailable
                ? "${_api.completedRitasiCount} TERCATAT"
                : '-',
            valueColor: FmsTheme.amberWarning,
          ),
        ),
        const SizedBox(width: 4),

        // GPS UTM Position
        Expanded(
          flex: 25,
          child: TelemetryCapsule(
            icon: "🌐",
            label: _api.navigationIsHeld && !_api.hasUnitGpsFix
                ? "UTM TERAKHIR"
                : "UTM",
            value: _api.hasDisplayGpsFix
                ? "${_api.hdEasting.toStringAsFixed(0)}E, ${_api.hdNorthing.toStringAsFixed(0)}N"
                : '-',
            valueColor: FmsTheme.cyanAccent,
          ),
        ),
        const SizedBox(width: 4),

        // Target EXA / DUMP
        Expanded(
          flex: 28,
          child: TelemetryCapsule(
            icon:
                _api.activeTargetType.contains("Dump") ||
                    _api.activeTargetType.contains("Disposal")
                ? "📍"
                : "🚜",
            label: "TARGET",
            value: _api.activeTargetName,
            valueColor: FmsTheme.cyanAccent,
          ),
        ),
        const SizedBox(width: 4),

        // Bearing Direction
        Expanded(
          flex: 17,
          child: TelemetryCapsule(
            icon: "🧭",
            label: "ARAH",
            value: _api.relativeDirectionLabel,
            valueColor: FmsTheme.emeraldGreen,
          ),
        ),
        const SizedBox(width: 4),

        // Source age for the selected unit.
        Expanded(
          flex: 22,
          child: TelemetryCapsule(
            icon: "📐",
            label: "UMUR GPS",
            value: _api.selectedUnit?.lastHeardSecondsAgo != null
                ? "${_api.selectedUnit!.lastHeardSecondsAgo} s"
                : '-',
            valueColor: FmsTheme.cyanAccent,
          ),
        ),
      ],
    );
  }
}

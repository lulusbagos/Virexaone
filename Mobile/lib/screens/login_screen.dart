import 'package:flutter/material.dart';

import '../models/fleet_models.dart';
import '../services/fms_api_service.dart';
import '../theme/fms_theme.dart';
import '../widgets/server_settings_dialog.dart';
import 'fms_menu_screen.dart';

class LoginScreen extends StatefulWidget {
  final VoidCallback onLoginSuccess;
  const LoginScreen({super.key, required this.onLoginSuccess});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final FmsApiService api = FmsApiService();
  final TextEditingController search = TextEditingController();
  String? selectedName;
  bool onlyFresh = true;

  @override
  void dispose() {
    search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final freshCount = api.haulerUnits.where((unit) => unit.hasFreshGps).length;
    final units = api.haulerUnits.toList()
      ..sort((a, b) {
        if (a.hasFreshGps != b.hasFreshGps) return a.hasFreshGps ? -1 : 1;
        return a.unitName.compareTo(b.unitName);
      });
    final query = search.text.trim().toUpperCase();
    final visible = units
        .where(
          (unit) =>
              (!onlyFresh || unit.hasFreshGps) &&
              (query.isEmpty || unit.unitName.toUpperCase().contains(query)),
        )
        .toList();
    final selected = visible.where((unit) => unit.unitName == selectedName).firstOrNull;

    return Scaffold(
      backgroundColor: const Color(0xFF0D1517),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1180),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(18, 10, 18, 12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _header(),
                  const SizedBox(height: 10),
                  _connectionBar(freshCount),
                  const SizedBox(height: 10),
                  Expanded(
                    child: LayoutBuilder(
                      builder: (context, size) => size.maxWidth < 690
                          ? _unitList(visible)
                          : Row(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                Expanded(child: _unitList(visible)),
                                const SizedBox(width: 14),
                                SizedBox(
                                  width: 250,
                                  child: _unitDetails(selected),
                                ),
                              ],
                            ),
                    ),
                  ),
                  const SizedBox(height: 9),
                  _footer(selected),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _header() => Row(
    children: [
      Container(
        width: 44,
        height: 44,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          border: Border.all(color: FmsTheme.emeraldGreen),
          color: const Color(0xFF15302D),
        ),
        child: const Icon(
          Icons.precision_manufacturing_outlined,
          color: FmsTheme.emeraldGreen,
          size: 26,
        ),
      ),
      const SizedBox(width: 12),
      Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'VIREXA ONE',
              style: FmsTheme.titleLarge.copyWith(fontSize: 21),
            ),
            Text(
              'ASTHA VIREXA TECHNOLOGY  /  KABIN',
              style: FmsTheme.caption.copyWith(
                color: FmsTheme.emeraldGreen,
                fontSize: 9,
              ),
            ),
          ],
        ),
      ),
      IconButton(
        tooltip: 'Menu FMS',
        onPressed: () => showDialog<void>(
          context: context,
          builder: (_) => const FmsMenuScreen(),
        ),
        icon: const Icon(Icons.menu, color: FmsTheme.textLight),
      ),
      IconButton(
        tooltip: 'Pengaturan server',
        onPressed: () => showDialog<void>(
          context: context,
          builder: (_) => const ServerSettingsDialog(),
        ),
        icon: const Icon(Icons.settings_outlined, color: FmsTheme.textLight),
      ),
    ],
  );

  Widget _connectionBar(int freshCount) => Container(
    constraints: const BoxConstraints(minHeight: 44),
    padding: const EdgeInsets.symmetric(horizontal: 11),
    decoration: BoxDecoration(
      color: api.isApiConnected
          ? const Color(0xFF142622)
          : const Color(0xFF302619),
      border: Border.all(
        color: api.isApiConnected
            ? FmsTheme.emeraldGreen.withValues(alpha: 0.5)
            : FmsTheme.amberWarning.withValues(alpha: 0.6),
      ),
    ),
    child: Row(
      children: [
        Icon(
          api.isApiConnected ? Icons.cloud_done_outlined : Icons.cloud_off,
          size: 18,
          color: api.isApiConnected
              ? FmsTheme.emeraldGreen
              : FmsTheme.amberWarning,
        ),
        const SizedBox(width: 9),
        Expanded(
          child: Text(
            api.isApiConnected
                ? 'DATA TERHUBUNG  /  ${api.haulerUnits.length} HAULER  /  $freshCount GPS AKTIF'
                : 'KONEKSI DATA TERPUTUS  /  ${api.apiStatusMessage}',
            style: FmsTheme.bodyNormal,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
        ),
        IconButton(
          tooltip: 'Segarkan data',
          onPressed: api.syncFromBackend,
          icon: const Icon(Icons.refresh, color: FmsTheme.cyanAccent),
        ),
      ],
    ),
  );

  Widget _unitList(List<FleetUnit> units) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Row(
        children: [
          Expanded(
            child: SizedBox(
              height: 40,
              child: TextField(
                controller: search,
                onChanged: (_) => setState(() {}),
                style: FmsTheme.bodyNormal,
                decoration: InputDecoration(
                  hintText: 'Cari nomor unit',
                  hintStyle: FmsTheme.caption,
                  prefixIcon: const Icon(Icons.search, size: 18),
                  suffixIcon: search.text.isEmpty
                      ? null
                      : IconButton(
                          tooltip: 'Hapus pencarian',
                          onPressed: () => setState(search.clear),
                          icon: const Icon(Icons.close, size: 17),
                        ),
                  filled: true,
                  fillColor: const Color(0xFF18252A),
                  contentPadding: const EdgeInsets.symmetric(vertical: 8),
                  border: const OutlineInputBorder(),
                ),
              ),
            ),
          ),
          const SizedBox(width: 8),
          FilterChip(
            label: const Text('GPS aktif'),
            selected: onlyFresh,
            onSelected: (value) => setState(() => onlyFresh = value),
            avatar: const Icon(Icons.gps_fixed, size: 15),
            visualDensity: VisualDensity.compact,
          ),
        ],
      ),
      const SizedBox(height: 7),
      Expanded(
        child: units.isEmpty
            ? _emptyFleet()
            : ListView.builder(
                itemCount: units.length,
                itemBuilder: (context, index) {
                  final unit = units[index];
                  final chosen = unit.unitName == selectedName;
                  return Material(
                    color: chosen
                        ? const Color(0xFF18312F)
                        : Colors.transparent,
                    child: InkWell(
                      onTap: () {
                          if (selectedName == unit.unitName) {
                            if (api.login('', '', unit.unitName)) {
                              widget.onLoginSuccess();
                            }
                          } else {
                            setState(() => selectedName = unit.unitName);
                          }
                        },
                      child: Container(
                        height: 54,
                        padding: const EdgeInsets.symmetric(horizontal: 10),
                        decoration: BoxDecoration(
                          border: Border(
                            left: BorderSide(
                              color: chosen
                                  ? FmsTheme.emeraldGreen
                                  : Colors.transparent,
                              width: 3,
                            ),
                            bottom: const BorderSide(color: Color(0xFF29373B)),
                          ),
                        ),
                        child: Row(
                          children: [
                            Icon(
                              Icons.local_shipping_outlined,
                              size: 19,
                              color: unit.hasFreshGps
                                  ? FmsTheme.emeraldGreen
                                  : FmsTheme.textMuted,
                            ),
                            const SizedBox(width: 9),
                            Expanded(
                              child: Column(
                                mainAxisAlignment: MainAxisAlignment.center,
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    unit.unitName,
                                    style: FmsTheme.titleMedium,
                                  ),
                                  Text(
                                    unit.activityName,
                                    style: FmsTheme.caption,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ],
                              ),
                            ),
                            Text(
                              unit.hasFreshGps
                                  ? 'GPS ${unit.lastHeardSecondsAgo}s'
                                  : 'GPS lama',
                              style: FmsTheme.caption.copyWith(
                                color: unit.hasFreshGps
                                    ? FmsTheme.emeraldGreen
                                    : FmsTheme.amberWarning,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                },
              ),
      ),
    ],
  );

  Widget _emptyFleet() {
    final connected = api.isApiConnected;
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 330),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 54,
              height: 54,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: connected
                    ? const Color(0xFF18312F)
                    : const Color(0xFF302619),
                border: Border.all(
                  color: connected
                      ? FmsTheme.emeraldGreen
                      : FmsTheme.amberWarning,
                ),
              ),
              child: Icon(
                connected ? Icons.search_off : Icons.cloud_off_outlined,
                size: 26,
                color: connected
                    ? FmsTheme.emeraldGreen
                    : FmsTheme.amberWarning,
              ),
            ),
            const SizedBox(height: 14),
            Text(
              connected ? 'Tidak ada unit yang cocok' : 'Data armada belum tersedia',
              textAlign: TextAlign.center,
              style: FmsTheme.titleLarge.copyWith(fontSize: 17),
            ),
            const SizedBox(height: 7),
            Text(
              connected
                  ? 'Periksa pencarian atau filter GPS.'
                  : api.apiStatusMessage,
              textAlign: TextAlign.center,
              style: FmsTheme.bodyNormal.copyWith(color: FmsTheme.textMuted),
            ),
            if (!connected) ...[
              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: api.syncFromBackend,
                icon: const Icon(Icons.refresh, size: 18),
                label: const Text('Coba lagi'),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _unitDetails(FleetUnit? unit) => Container(
    padding: const EdgeInsets.all(12),
    decoration: const BoxDecoration(
      color: Color(0xFF182126),
      border: Border(top: BorderSide(color: FmsTheme.emeraldGreen, width: 2)),
    ),
    child: unit == null
        ? Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'UNIT TERPILIH',
                style: FmsTheme.caption.copyWith(color: FmsTheme.emeraldGreen),
              ),
              const Spacer(),
              Center(
                child: Column(
                  children: [
                    Icon(
                      api.isApiConnected
                          ? Icons.local_shipping_outlined
                          : Icons.sensors_off_outlined,
                      size: 32,
                      color: FmsTheme.textMuted,
                    ),
                    const SizedBox(height: 10),
                    Text(
                      api.isApiConnected ? 'Pilih unit kabin' : 'Menunggu data',
                      style: FmsTheme.titleMedium,
                    ),
                  ],
                ),
              ),
              const Spacer(),
              Text(
                api.isApiConnected
                    ? 'Pilih hauler dengan GPS aktif.'
                    : 'Status unit tampil setelah koneksi pulih.',
                style: FmsTheme.caption,
              ),
            ],
          )
        : Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'UNIT TERPILIH',
                style: FmsTheme.caption.copyWith(color: FmsTheme.emeraldGreen),
              ),
              Text(
                unit.unitName,
                style: FmsTheme.titleLarge.copyWith(fontSize: 25),
              ),
              Text(unit.unitType, style: FmsTheme.caption),
              const Spacer(),
              _detail('Aktivitas', unit.activityName),
              _detail(
                'Kecepatan',
                unit.hasFreshGps
                    ? '${unit.speedKmh.toStringAsFixed(1)} km/jam'
                    : '-',
              ),
              _detail(
                'Heading',
                unit.hasNavigationHeading
                    ? '${unit.headingDeg.toStringAsFixed(0)}°'
                    : 'Tidak tersedia',
              ),
              _detail(
                'GPS',
                unit.hasFreshGps
                    ? '${unit.lastHeardSecondsAgo} detik lalu'
                    : 'Data lama',
              ),
            ],
          ),
  );

  Widget _detail(String label, String value) => Padding(
    padding: const EdgeInsets.only(top: 5),
    child: Row(
      children: [
        SizedBox(width: 65, child: Text(label, style: FmsTheme.caption)),
        Expanded(
          child: Text(
            value,
            style: FmsTheme.bodyNormal,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    ),
  );

  Widget _footer(FleetUnit? selected) => Row(
    children: [
      Expanded(
        child: Text(
          'Pemilihan unit bukan autentikasi operator.',
          style: FmsTheme.caption,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
        ),
      ),
      const SizedBox(width: 8),
      SizedBox(
        height: 42,
        child: FilledButton.icon(
          onPressed: selected != null
              ? () {
                  if (api.login('', '', selected.unitName)) {
                    widget.onLoginSuccess();
                  }
                }
              : null,
          style: FilledButton.styleFrom(
            backgroundColor: selected != null ? FmsTheme.emeraldGreen : null,
            foregroundColor: FmsTheme.bgDark,
            textStyle: const TextStyle(fontWeight: FontWeight.w900, letterSpacing: 0.5),
          ),
          icon: const Icon(Icons.arrow_forward, size: 18),
          label: const Text('BUKA KABIN'),
        ),
      ),
    ],
  );
}

import 'package:flutter/material.dart';

import '../services/fms_api_service.dart';
import '../services/fms_settings_service.dart';
import '../theme/fms_theme.dart';
import 'cabin_comms_pane.dart';

enum _MenuPage {
  operation,
  assignment,
  fleet,
  messages,
  locations,
  diagnostics,
  settings,
  hexagon,
  roadmap,
}

class FmsMenuScreen extends StatefulWidget {
  const FmsMenuScreen({super.key, this.openMessages = false});
  final bool openMessages;

  @override
  State<FmsMenuScreen> createState() => _FmsMenuScreenState();
}

class _FmsMenuScreenState extends State<FmsMenuScreen> {
  final FmsApiService api = FmsApiService();
  final FmsSettingsService settings = FmsSettingsService();
  _MenuPage page = _MenuPage.operation;
  final fleetSearch = TextEditingController();
  final locationSearch = TextEditingController();
  bool onlyFreshFleet = true;

  @override
  void initState() {
    super.initState();
    if (widget.openMessages) page = _MenuPage.messages;
  }

  @override
  void dispose() {
    fleetSearch.dispose();
    locationSearch.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Dialog(
      insetPadding: const EdgeInsets.all(10),
      backgroundColor: FmsTheme.bgDark,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(10),
        side: BorderSide(color: FmsTheme.cardBorder, width: 1.5),
      ),
      child: Material(
        color: FmsTheme.bgDark,
        borderRadius: BorderRadius.circular(10),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 1100, maxHeight: 680),
          child: ListenableBuilder(
            listenable: Listenable.merge([api, settings]),
            builder: (context, _) => Column(
              children: [
                _header(),
                Divider(height: 1, color: FmsTheme.cardBorder),
                Expanded(
                  child: LayoutBuilder(
                    builder: (context, size) => size.maxWidth < 650
                        ? Column(
                            children: [
                              SizedBox(
                                height: 56,
                                child: _tabs(horizontal: true),
                              ),
                              Expanded(child: _content()),
                            ],
                          )
                        : Row(
                            children: [
                              SizedBox(
                                width: 200,
                                child: _tabs(horizontal: false),
                              ),
                              VerticalDivider(
                                width: 1,
                                color: FmsTheme.cardBorder,
                              ),
                              Expanded(child: _content()),
                            ],
                          ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _header() => Padding(
    padding: const EdgeInsets.fromLTRB(16, 10, 8, 10),
    child: Row(
      children: [
        Icon(
          Icons.dashboard_customize_outlined,
          color: FmsTheme.emeraldGreen,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${settings.tr('app_title')} / ${settings.tr('cockpit_title')}',
                style: FmsTheme.titleMedium.copyWith(fontSize: 15, fontWeight: FontWeight.w800),
              ),
              Text('${api.selectedUnitId} • Astha Mining Pit', style: FmsTheme.caption),
            ],
          ),
        ),
        IconButton(
          tooltip: settings.tr('refresh'),
          onPressed: api.syncFromBackend,
          icon: Icon(Icons.refresh, color: FmsTheme.cyanAccent),
        ),
        IconButton(
          tooltip: settings.tr('close_btn'),
          onPressed: () => Navigator.of(context).pop(),
          icon: const Icon(Icons.close),
        ),
      ],
    ),
  );

  Widget _tabs({required bool horizontal}) {
    final entries = <(_MenuPage, IconData, String)>[
      (_MenuPage.operation, Icons.precision_manufacturing_outlined, settings.tr('tab_operation')),
      (_MenuPage.assignment, Icons.route_outlined, settings.tr('tab_assignment')),
      (_MenuPage.fleet, Icons.local_shipping_outlined, settings.tr('tab_fleet')),
      (_MenuPage.messages, Icons.chat_bubble_outline, settings.tr('tab_messages')),
      (_MenuPage.locations, Icons.place_outlined, settings.tr('tab_locations')),
      (_MenuPage.diagnostics, Icons.monitor_heart_outlined, settings.tr('tab_diagnostics')),
      (_MenuPage.settings, Icons.tune_rounded, settings.tr('tab_settings')),
      (_MenuPage.hexagon, Icons.data_object_outlined, settings.tr('tab_hexagon')),
      (_MenuPage.roadmap, Icons.grid_view_outlined, 'Roadmap FMS'),
    ];
    final children = entries.map((entry) {
      final selected = page == entry.$1;
      return TextButton.icon(
        onPressed: () => setState(() => page = entry.$1),
        style: TextButton.styleFrom(
          foregroundColor: selected
              ? FmsTheme.emeraldGreen
              : FmsTheme.textMuted,
          backgroundColor: selected
              ? FmsTheme.cardHeaderBg
              : Colors.transparent,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(6)),
          minimumSize: horizontal ? const Size(120, 48) : const Size(190, 44),
          alignment: Alignment.centerLeft,
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        ),
        icon: Icon(entry.$2, size: 18),
        label: Text(
          entry.$3,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
            fontSize: 12,
          ),
        ),
      );
    }).toList();
    return horizontal
        ? ListView(scrollDirection: Axis.horizontal, children: children)
        : ListView(
            padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 6),
            children: children,
          );
  }

  Widget _content() {
    final Widget body;
    switch (page) {
      case _MenuPage.operation:
        body = _operation();
      case _MenuPage.assignment:
        body = _assignment();
      case _MenuPage.fleet:
        body = _fleet();
      case _MenuPage.messages:
        body = const CabinCommsPane();
      case _MenuPage.locations:
        body = _locations();
      case _MenuPage.diagnostics:
        body = _diagnostics();
      case _MenuPage.settings:
        body = _settings();
      case _MenuPage.hexagon:
        body = _hexagon();
      case _MenuPage.roadmap:
        body = _roadmap();
    }
    return Padding(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Expanded(child: body),
          Divider(color: FmsTheme.cardBorder),
          Text(
            api.isApiConnected
                ? 'Sumber: API FMS / data Hexagon (baca saja)  |  sinkron ${_time(api.lastApiSyncTime)}'
                : api.apiStatusMessage,
            style: FmsTheme.caption.copyWith(
              color: api.isApiConnected
                  ? FmsTheme.textMuted
                  : FmsTheme.amberWarning,
            ),
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }

  // =========================================================================
  // SETTINGS TAB: DYNAMIC THEME & LANGUAGE CONTROLLER
  // =========================================================================
  Widget _settings() {
    return ListView(
      children: [
        _title(settings.tr('settings_title'), settings.tr('settings_subtitle')),
        const SizedBox(height: 6),

        // Section 1: Themes
        Text(
          settings.tr('theme_section_title'),
          style: TextStyle(
            color: FmsTheme.cyanAccent,
            fontWeight: FontWeight.w800,
            fontSize: 12,
            letterSpacing: 0.5,
          ),
        ),
        const SizedBox(height: 10),

        _buildThemeOptionCard(
          mode: AppThemeMode.midnightCyber,
          title: settings.tr('theme_midnight_title'),
          description: settings.tr('theme_midnight_desc'),
          icon: Icons.nightlight_round_rounded,
          previewColors: [const Color(0xFF030712), const Color(0xFF00FFA3), const Color(0xFF00E5FF)],
        ),
        const SizedBox(height: 8),

        _buildThemeOptionCard(
          mode: AppThemeMode.solarDay,
          title: settings.tr('theme_solar_title'),
          description: settings.tr('theme_solar_desc'),
          icon: Icons.wb_sunny_rounded,
          previewColors: [const Color(0xFF0A121E), const Color(0xFF00FFB2), const Color(0xFF00D8F6)],
        ),
        const SizedBox(height: 8),

        _buildThemeOptionCard(
          mode: AppThemeMode.amberWarmth,
          title: settings.tr('theme_amber_title'),
          description: settings.tr('theme_amber_desc'),
          icon: Icons.shield_moon_rounded,
          previewColors: [const Color(0xFF0B0803), const Color(0xFFFFB300), const Color(0xFFFFD54F)],
        ),
        const SizedBox(height: 8),

        _buildThemeOptionCard(
          mode: AppThemeMode.emeraldTactical,
          title: settings.tr('theme_emerald_title'),
          description: settings.tr('theme_emerald_desc'),
          icon: Icons.radar_rounded,
          previewColors: [const Color(0xFF020B07), const Color(0xFF00FFA3), const Color(0xFF64FFDA)],
        ),

        const SizedBox(height: 20),
        Divider(color: FmsTheme.cardBorder),
        const SizedBox(height: 10),

        // Section 2: Languages
        Text(
          settings.tr('lang_section_title'),
          style: TextStyle(
            color: FmsTheme.cyanAccent,
            fontWeight: FontWeight.w800,
            fontSize: 12,
            letterSpacing: 0.5,
          ),
        ),
        const SizedBox(height: 10),

        Row(
          children: [
            Expanded(
              child: _buildLanguageCard(
                language: AppLanguage.id,
                flag: '🇮🇩',
                title: settings.tr('lang_id_title'),
                description: settings.tr('lang_id_desc'),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _buildLanguageCard(
                language: AppLanguage.en,
                flag: '🇬🇧',
                title: settings.tr('lang_en_title'),
                description: settings.tr('lang_en_desc'),
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
      ],
    );
  }

  Widget _buildThemeOptionCard({
    required AppThemeMode mode,
    required String title,
    required String description,
    required IconData icon,
    required List<Color> previewColors,
  }) {
    final isSelected = settings.currentTheme == mode;

    return InkWell(
      onTap: () => settings.setTheme(mode),
      borderRadius: BorderRadius.circular(8),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: isSelected ? FmsTheme.cardHeaderBg : FmsTheme.cardBg,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(
            color: isSelected ? FmsTheme.emeraldGreen : FmsTheme.cardBorder,
            width: isSelected ? 1.8 : 1.0,
          ),
          boxShadow: isSelected ? FmsTheme.neonGlowShadow(FmsTheme.emeraldGreen, opacity: 0.25) : null,
        ),
        child: Row(
          children: [
            Container(
              width: 38,
              height: 38,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: previewColors[0],
                border: Border.all(color: previewColors[1], width: 1.5),
              ),
              child: Icon(icon, color: previewColors[1], size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Text(
                        title,
                        style: TextStyle(
                          color: isSelected ? FmsTheme.emeraldGreen : FmsTheme.textLight,
                          fontWeight: FontWeight.w700,
                          fontSize: 13,
                        ),
                      ),
                      if (isSelected) ...[
                        const SizedBox(width: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                          decoration: BoxDecoration(
                            color: FmsTheme.emeraldGreen.withValues(alpha: 0.2),
                            borderRadius: BorderRadius.circular(4),
                            border: Border.all(color: FmsTheme.emeraldGreen),
                          ),
                          child: const Text(
                            'AKTIF',
                            style: TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.w900,
                              fontSize: 9,
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                  const SizedBox(height: 3),
                  Text(description, style: FmsTheme.caption.copyWith(fontSize: 10)),
                ],
              ),
            ),
            const SizedBox(width: 8),
            // Color Dots
            Row(
              mainAxisSize: MainAxisSize.min,
              children: previewColors
                  .map(
                    (c) => Container(
                      margin: const EdgeInsets.only(left: 4),
                      width: 12,
                      height: 12,
                      decoration: BoxDecoration(
                        color: c,
                        shape: BoxShape.circle,
                        border: Border.all(color: Colors.white30, width: 0.8),
                      ),
                    ),
                  )
                  .toList(),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildLanguageCard({
    required AppLanguage language,
    required String flag,
    required String title,
    required String description,
  }) {
    final isSelected = settings.currentLanguage == language;

    return InkWell(
      onTap: () => settings.setLanguage(language),
      borderRadius: BorderRadius.circular(8),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: isSelected ? FmsTheme.cardHeaderBg : FmsTheme.cardBg,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(
            color: isSelected ? FmsTheme.cyanAccent : FmsTheme.cardBorder,
            width: isSelected ? 1.8 : 1.0,
          ),
          boxShadow: isSelected ? FmsTheme.neonGlowShadow(FmsTheme.cyanAccent, opacity: 0.25) : null,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Text(flag, style: const TextStyle(fontSize: 18)),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    title,
                    style: TextStyle(
                      color: isSelected ? FmsTheme.cyanAccent : FmsTheme.textLight,
                      fontWeight: FontWeight.w700,
                      fontSize: 12.5,
                    ),
                  ),
                ),
                if (isSelected)
                  Icon(Icons.check_circle_rounded, color: FmsTheme.cyanAccent, size: 18),
              ],
            ),
            const SizedBox(height: 4),
            Text(description, style: FmsTheme.caption.copyWith(fontSize: 9.5)),
          ],
        ),
      ),
    );
  }

  Widget _operation() {
    final unit = api.selectedUnit;
    if (unit == null) return _empty('Unit tidak tersedia dari API.');
    return ListView(
      children: [
        _title('Aktivitas unit', 'Status dan GPS unit yang dipilih'),
        _line('Unit', unit.unitName),
        _line('Tipe', unit.unitType),
        _line('Aktivitas', unit.activityName),
        _line('Status ID sumber', unit.statusId?.toString() ?? '-'),
        _line(
          'Kecepatan',
          unit.hasFreshGps ? '${unit.speedKmh.toStringAsFixed(1)} km/jam' : '-',
        ),
        _line('GPS terakhir', unit.lastHeard ?? '-'),
        _line(
          'Kualitas GPS',
          unit.hasFreshGps
              ? 'Aktif / ${unit.lastHeardSecondsAgo}s lalu'
              : 'Data lama atau kosong',
        ),
        _line(
          'Muatan',
          unit.payloadAvailable
              ? '${unit.payloadTon?.toStringAsFixed(1) ?? '-'} ton'
              : 'Tidak tersedia',
        ),
        _line(
          'Total dump',
          unit.haulDataAvailable ? '${unit.recordedLoads ?? 0} ritase' : 'Tidak tersedia',
        ),
        _line('Rute / penugasan', unit.assignedShovelName ?? 'Belum ada assignment'),
      ],
    );
  }

  Widget _assignment() {
    final list = api.activeDispatches;
    if (list.isEmpty) return _empty('Belum ada penugasan aktif dari Dispatcher.');
    return ListView.builder(
      itemCount: list.length,
      itemBuilder: (context, idx) {
        final d = list[idx];
        return Container(
          margin: const EdgeInsets.only(bottom: 8),
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            color: FmsTheme.cardBg,
            borderRadius: BorderRadius.circular(6),
            border: Border.all(color: FmsTheme.cardBorder),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Penugasan: ${d.truckName} ➔ ${d.shovelName}',
                style: FmsTheme.titleMedium,
              ),
              const SizedBox(height: 4),
              Text('Lokasi Dump: ${d.locationName}', style: FmsTheme.caption),
            ],
          ),
        );
      },
    );
  }

  Widget _fleet() {
    final fleet = api.allUnits;
    if (fleet.isEmpty) return _empty('Tidak ada data armada online.');
    return ListView.builder(
      itemCount: fleet.length,
      itemBuilder: (context, idx) {
        final u = fleet[idx];
        return ListTile(
          dense: true,
          leading: Icon(
            u.unitType.toLowerCase().contains('shovel') || u.unitType.toLowerCase().contains('excavator')
                ? Icons.engineering
                : Icons.local_shipping,
            color: FmsTheme.cyanAccent,
          ),
          title: Text(u.unitName, style: FmsTheme.titleMedium),
          subtitle: Text('${u.unitType} • ${u.activityName}', style: FmsTheme.caption),
          trailing: Text('${u.speedKmh.toStringAsFixed(0)} km/h', style: FmsTheme.codePill),
        );
      },
    );
  }

  Widget _locations() {
    final locs = api.miningLocations;
    if (locs.isEmpty) return _empty('Data geofence lokasi pit belum dimuat.');
    return ListView.builder(
      itemCount: locs.length,
      itemBuilder: (context, idx) {
        final loc = locs[idx];
        return ListTile(
          dense: true,
          leading: const Icon(Icons.pin_drop, color: FmsTheme.amberWarning),
          title: Text(loc.name, style: FmsTheme.titleMedium),
          subtitle: Text('Tipe: ${loc.type} • Elevasi: ${loc.elevation.toStringAsFixed(1)} m', style: FmsTheme.caption),
        );
      },
    );
  }

  Widget _diagnostics() {
    return ListView(
      children: [
        _title('Diagnostik ECU & Jaringan', 'Status sensor in-cabin dan telematika'),
        _line('Status Jaringan', api.isApiConnected ? 'ONLINE (4G LTE / Wi-Fi Mesh)' : 'OFFLINE'),
        _line('Protokol Comms', 'WebSocket PCM16 Half-Duplex (16 kHz)'),
        _line('ECU Controller', 'CAT ECM Telemetry Ready'),
        _line('Sensor GPS/IMU', 'Dual-Frequency RTK GNSS (±2cm accuracy)'),
        _line('Kamera Fatigue (Savera FTW)', 'Aktif • AI Eye Tracker OK'),
      ],
    );
  }

  Widget _hexagon() {
    final data = api.hexagonUnitData;
    if (data == null) return _empty(api.hexagonDataStatus);
    String value(String key) => data[key]?.toString() ?? 'Tidak tersedia';
    String time(String key) {
      final parsed = DateTime.tryParse(value(key));
      return parsed == null ? 'Tidak tersedia' : parsed.toLocal().toString().split('.').first;
    }
    return ListView(children: [
      _title('Data sumber / ${api.selectedUnitId}',
          'Snapshot baca saja dari tabel Hexagon; ID mengikuti kode sumber'),
      _line('Jenis peralatan', value('equipment_type')),
      _line('Status / aktivitas', '${value('status_id')} / ${value('activity_id')}'),
      _line('Prestart', data['prestart_check'] == null
          ? 'Tidak tersedia' : data['prestart_check'] == true ? 'Tercatat: ya' : 'Tercatat: tidak'),
      _line('Kode peringatan', value('warnings_code')),
      _line('GPS tercatat', time('gps_located_at')),
      _line('Update traveling', time('travel_updated_at')),
      _line('Satelit', value('satellites')),
      _line('HDOP / sinyal (raw)', '${value('hdop_raw')} / ${value('signal_strength_raw')}'),
      _line('Kode kualitas GPS', value('gps_quality_id')),
      _line('Ruas jalan ID', value('road_segment_id')),
      _line('Lokasi sekarang', value('current_location_name')),
      _line('Lokasi berikutnya', value('next_location_name')),
      _line('Update assignment', time('assignment_updated_at')),
      _line('Update hauling', time('haul_updated_at')),
      _line('Total load sumber', value('total_loads')),
      _line('Tonase sumber', value('tonnage')),
      _line('Muatan terdeteksi', data['has_payload'] == null
          ? 'Tidak tersedia' : data['has_payload'] == true ? 'Ya' : 'Tidak'),
      _line('ID material / shovel / dump',
          '${value('material_id')} / ${value('haul_shovel_id')} / ${value('haul_dump_id')}'),
    ]);
  }

  Widget _roadmap() => ListView(
    children: [
      _title('Modul FMS', 'Kesiapan data dan integrasi kabin'),
      _line('Aktivitas / GPS / penugasan', 'Data aktual - baca saja'),
      _line('MTC / peta / rekayasa jalan', 'Tetap di UI pengendali'),
      _line('Peta GPS kabin', 'Menunggu layer peta dan jalan tervalidasi'),
      _line('Status & alasan delay', 'Menunggu katalog alasan dan API perintah'),
      _line('Pesan kabin', 'Teks dan pesan suara masuk; live WebSocket PCM16 aktif'),
      _line('Darurat', 'Sinyal Mayday & Notifikasi otomatis siap'),
      _line('Prestart & defect', 'Pemeriksaan P2H & sensor ban'),
      _line('Hazard jalan', 'Deteksi kemiringan & rute aman'),
    ],
  );

  Widget _title(String title, String subtitle) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: FmsTheme.titleMedium.copyWith(fontSize: 16, fontWeight: FontWeight.w800)),
        const SizedBox(height: 2),
        Text(subtitle, style: FmsTheme.caption),
      ],
    ),
  );

  Widget _line(String label, String value) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 7),
    child: LayoutBuilder(
      builder: (context, constraints) => Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: (constraints.maxWidth * 0.38).clamp(90.0, 160.0),
            child: Text(label, style: FmsTheme.caption),
          ),
          Expanded(child: Text(value, style: FmsTheme.bodyNormal)),
        ],
      ),
    ),
  );

  Widget _empty(String message) =>
      Center(child: Text(message, style: FmsTheme.caption));

  String _time(DateTime? date) => date == null
      ? '-'
      : '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}:${date.second.toString().padLeft(2, '0')}';
}

import 'package:flutter/material.dart';

import '../services/fms_api_service.dart';
import '../theme/fms_theme.dart';
import 'cabin_comms_pane.dart';
import '../widgets/select_target_dialog.dart';

enum _MenuPage {
  operation,
  assignment,
  fleet,
  messages,
  locations,
  diagnostics,
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
        borderRadius: BorderRadius.circular(6),
        side: const BorderSide(color: FmsTheme.cardBorder),
      ),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1100, maxHeight: 680),
        child: AnimatedBuilder(
          animation: api,
          builder: (context, _) => Column(
            children: [
              _header(),
              const Divider(height: 1, color: FmsTheme.cardBorder),
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
                              width: 190,
                              child: _tabs(horizontal: false),
                            ),
                            const VerticalDivider(
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
    );
  }

  Widget _header() => Padding(
    padding: const EdgeInsets.fromLTRB(16, 9, 8, 9),
    child: Row(
      children: [
        const Icon(
          Icons.dashboard_customize_outlined,
          color: FmsTheme.emeraldGreen,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'FMS / KABIN',
                style: FmsTheme.titleMedium.copyWith(fontSize: 16),
              ),
              Text(api.selectedUnitId, style: FmsTheme.caption),
            ],
          ),
        ),
        IconButton(
          tooltip: 'Segarkan data',
          onPressed: api.syncFromBackend,
          icon: const Icon(Icons.refresh, color: FmsTheme.cyanAccent),
        ),
        IconButton(
          tooltip: 'Tutup menu',
          onPressed: () => Navigator.of(context).pop(),
          icon: const Icon(Icons.close),
        ),
      ],
    ),
  );

  Widget _tabs({required bool horizontal}) {
    const entries = <(_MenuPage, IconData, String)>[
      (_MenuPage.operation, Icons.precision_manufacturing_outlined, 'Operasi'),
      (_MenuPage.assignment, Icons.route_outlined, 'Penugasan'),
      (_MenuPage.fleet, Icons.local_shipping_outlined, 'Armada'),
      (_MenuPage.messages, Icons.chat_bubble_outline, 'Pesan'),
      (_MenuPage.locations, Icons.place_outlined, 'Lokasi'),
      (_MenuPage.diagnostics, Icons.monitor_heart_outlined, 'Diagnostik'),
      (_MenuPage.hexagon, Icons.data_object_outlined, 'Data sumber'),
      (_MenuPage.roadmap, Icons.grid_view_outlined, 'Kesiapan'),
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
          shape: const RoundedRectangleBorder(),
          minimumSize: horizontal ? const Size(115, 48) : const Size(180, 46),
          alignment: Alignment.centerLeft,
        ),
        icon: Icon(entry.$2, size: 18),
        label: Text(entry.$3, maxLines: 1, overflow: TextOverflow.ellipsis),
      );
    }).toList();
    return horizontal
        ? ListView(scrollDirection: Axis.horizontal, children: children)
        : ListView(
            padding: const EdgeInsets.symmetric(vertical: 8),
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
          const Divider(color: FmsTheme.cardBorder),
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
          'Load tercatat',
          unit.haulDataAvailable
              ? '${unit.recordedLoads ?? '-'}'
              : 'Tidak tersedia',
        ),
      ],
    );
  }

  Widget _assignment() {
    final unit = api.selectedUnit;
    final matches = api.activeDispatches
        .where(
          (item) =>
              (unit != null && item.truckId == unit.unitId) ||
              (api.selectedUnitId.isNotEmpty &&
                  item.truckName.toUpperCase() ==
                      api.selectedUnitId.toUpperCase()),
        )
        .toList();
    matches.sort((a, b) => (b.updatedAt ?? '').compareTo(a.updatedAt ?? ''));
    final assignment = matches.firstOrNull;
    return ListView(
      children: [
        _title('Penugasan saat ini', 'Hanya snapshot assignment dari FMS'),
        if (assignment == null)
          _empty('Belum ada assignment dari API.')
        else ...[
          _line(
            'Excavator',
            assignment.shovelName.isEmpty ? '-' : assignment.shovelName,
          ),
          _line(
            'Tujuan',
            assignment.locationName.isEmpty ? '-' : assignment.locationName,
          ),
          _line('Diperbarui', assignment.updatedAt ?? '-'),
          _line(
            'Kualitas',
            assignment.isRecent
                ? 'Dalam jendela 12 jam'
                : 'Assignment lama - verifikasi dispatcher',
          ),
        ],
        const SizedBox(height: 14),
        _line('Target navigasi', api.hasTarget ? api.activeTargetName : '-'),
        _line(
          'Arah',
          api.hasNavigationFix
              ? '${api.relativeBearingDegrees.toStringAsFixed(0)}° relatif'
              : 'Tidak tersedia',
        ),
        _line(
          'Jarak',
          api.hasNavigationFix
              ? '${api.distanceToTargetMeters.toStringAsFixed(0)} m garis lurus'
              : 'Tidak tersedia',
        ),
        const SizedBox(height: 12),
        Align(
          alignment: Alignment.centerLeft,
          child: OutlinedButton.icon(
            onPressed: () => showDialog<void>(
              context: context,
              builder: (_) => const SelectTargetDialog(),
            ),
            icon: const Icon(Icons.my_location, size: 18),
            label: const Text('Pilih target'),
          ),
        ),
      ],
    );
  }

  Widget _fleet() {
    final query = fleetSearch.text.trim().toLowerCase();
    final units =
        api.allUnits
            .where(
              (unit) =>
                  (!onlyFreshFleet || unit.hasFreshGps) &&
                  (query.isEmpty ||
                      unit.unitName.toLowerCase().contains(query) ||
                      unit.category.toLowerCase().contains(query) ||
                      unit.activityName.toLowerCase().contains(query)),
            )
            .toList()
          ..sort((a, b) => a.unitName.compareTo(b.unitName));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _title(
          'Armada',
          '${units.length} unit ditampilkan / ${api.allUnits.length} unit API',
        ),
        TextField(
          controller: fleetSearch,
          onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(
            isDense: true,
            hintText: 'Cari nomor, tipe, atau aktivitas unit',
            prefixIcon: Icon(Icons.search, size: 19),
          ),
        ),
        const SizedBox(height: 8),
        Align(
          alignment: Alignment.centerLeft,
          child: SegmentedButton<bool>(
            showSelectedIcon: false,
            segments: const [
              ButtonSegment(value: true, label: Text('GPS ≤2 menit')),
              ButtonSegment(value: false, label: Text('Semua')),
            ],
            selected: {onlyFreshFleet},
            onSelectionChanged: (selection) =>
                setState(() => onlyFreshFleet = selection.first),
          ),
        ),
        const SizedBox(height: 5),
        Expanded(
          child: units.isEmpty
              ? _empty('Tidak ada unit yang cocok.')
              : ListView.builder(
                  itemCount: units.length,
                  itemBuilder: (context, index) {
                    final unit = units[index];
                    return ListTile(
                      dense: true,
                      contentPadding: const EdgeInsets.symmetric(horizontal: 4),
                      leading: Icon(
                        unit.hasFreshGps ? Icons.gps_fixed : Icons.gps_off,
                        color: unit.hasFreshGps
                            ? FmsTheme.emeraldGreen
                            : FmsTheme.amberWarning,
                        size: 19,
                      ),
                      title: Text(unit.unitName, style: FmsTheme.bodyNormal),
                      subtitle: Text(
                        '${unit.category} / ${unit.activityName}',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: FmsTheme.caption,
                      ),
                      trailing: Text(
                        unit.hasFreshGps
                            ? '${unit.lastHeardSecondsAgo}s'
                            : 'GPS lama',
                        style: FmsTheme.caption,
                      ),
                    );
                  },
                ),
        ),
      ],
    );
  }

  Widget _locations() {
    final query = locationSearch.text.trim().toLowerCase();
    final locations =
        api.miningLocations
            .where(
              (location) =>
                  query.isEmpty ||
                  location.name.toLowerCase().contains(query) ||
                  location.category.toLowerCase().contains(query),
            )
            .toList()
          ..sort((a, b) => a.name.compareTo(b.name));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _title('Lokasi FMS', '${locations.length} lokasi ditampilkan'),
        TextField(
          controller: locationSearch,
          onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(
            isDense: true,
            hintText: 'Cari nama atau kategori lokasi',
            prefixIcon: Icon(Icons.search, size: 19),
          ),
        ),
        const SizedBox(height: 8),
        Expanded(
          child: locations.isEmpty
              ? _empty('Tidak ada lokasi yang cocok.')
              : ListView.builder(
                  itemCount: locations.length,
                  itemBuilder: (context, index) {
                    final location = locations[index];
                    final selected = location.name == api.activeTargetName;
                    return ListTile(
                      dense: true,
                      contentPadding: const EdgeInsets.symmetric(horizontal: 4),
                      leading: Icon(
                        selected ? Icons.my_location : Icons.place_outlined,
                        color: selected
                            ? FmsTheme.emeraldGreen
                            : FmsTheme.cyanAccent,
                        size: 19,
                      ),
                      title: Text(location.name, style: FmsTheme.bodyNormal),
                      subtitle: Text(
                        location.category.isEmpty
                            ? location.type
                            : location.category,
                        style: FmsTheme.caption,
                      ),
                      trailing: selected
                          ? const Icon(
                              Icons.check,
                              size: 17,
                              color: FmsTheme.emeraldGreen,
                            )
                          : null,
                    );
                  },
                ),
        ),
      ],
    );
  }

  Widget _diagnostics() {
    final unit = api.selectedUnit;
    return ListView(
      children: [
        _title('Kualitas data', 'Diagnostik koneksi dan penentuan posisi'),
        _line('API', api.isApiConnected ? 'Terhubung' : 'Terputus'),
        _line(
          'GPS unit',
          unit == null
              ? 'Tidak tersedia'
              : unit.hasFreshGps
              ? 'Aktif (${unit.lastHeardSecondsAgo}s)'
              : 'Lama / kosong',
        ),
        _line(
          'Heading GPS',
          unit?.hasNavigationHeading == true
              ? '${unit!.headingDeg.toStringAsFixed(0)}°'
              : 'Tidak tersedia',
        ),
        _line(
          'Posisi UTM',
          unit?.hasFreshGps == true
              ? '${unit!.easting.toStringAsFixed(1)} E / ${unit.northing.toStringAsFixed(1)} N'
              : 'Tidak tersedia',
        ),
        _line(
          'Assignment',
          api.activeDispatches.isEmpty
              ? 'Tidak tersedia'
              : '${api.activeDispatches.length} record API',
        ),
        _line(
          'Lokasi',
          api.miningLocations.isEmpty
              ? 'Tidak tersedia'
              : '${api.miningLocations.length} record API',
        ),
        const SizedBox(height: 8),
        Text(api.apiStatusMessage, style: FmsTheme.caption),
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
      _line(
        'Status & alasan delay',
        'Menunggu katalog alasan dan API perintah',
      ),
      _line('Pesan kabin', 'Teks dan pesan suara masuk; perlu aktivasi server'),
      _line('Darurat', 'Menunggu alur eskalasi dan audit'),
      _line('Prestart & defect', 'Menunggu formulir, aturan dan histori'),
      _line('Hazard jalan', 'Menunggu laporan dan validasi lokasi'),
      _line('KPI & maintenance', 'Menunggu read model tervalidasi'),
      _line('DozerHP', 'Modul khusus dozer; tidak aktif untuk hauler'),
    ],
  );

  Widget _title(String title, String subtitle) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: FmsTheme.titleMedium.copyWith(fontSize: 18)),
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
            width: (constraints.maxWidth * 0.38).clamp(90.0, 155.0),
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

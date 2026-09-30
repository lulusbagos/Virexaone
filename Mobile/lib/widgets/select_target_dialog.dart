import 'package:flutter/material.dart';

import '../theme/fms_theme.dart';
import '../services/fms_api_service.dart';

class SelectTargetDialog extends StatefulWidget {
  const SelectTargetDialog({super.key});

  @override
  State<SelectTargetDialog> createState() => _SelectTargetDialogState();
}

class _SelectTargetDialogState extends State<SelectTargetDialog>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final api = FmsApiService();
    final excavators = api.excavatorUnits
        .where((unit) => unit.hasFreshGps)
        .toList();
    final dumpingPins = api.dumpingPins.values.toList();

    return Dialog(
      backgroundColor: Colors.transparent,
      child: Container(
        width: (MediaQuery.sizeOf(context).width - 32).clamp(280.0, 680.0),
        height: (MediaQuery.sizeOf(context).height - 32).clamp(240.0, 420.0),
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: FmsTheme.surfaceDark,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: FmsTheme.cyanAccent, width: 1.5),
          boxShadow: FmsTheme.neonGlowShadow(FmsTheme.cyanAccent, opacity: 0.3),
        ),
        child: Column(
          children: [
            // Header
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    const Text("🎯 ", style: TextStyle(fontSize: 18)),
                    Text(
                      "PILIH TARGET NAVIGASI HUD 3D",
                      style: FmsTheme.titleMedium.copyWith(
                        color: FmsTheme.cyanAccent,
                        fontSize: 13,
                      ),
                    ),
                  ],
                ),
                IconButton(
                  onPressed: () => Navigator.of(context).pop(),
                  icon: Icon(
                    Icons.close,
                    color: FmsTheme.textMuted,
                    size: 18,
                  ),
                  padding: EdgeInsets.zero,
                  constraints: const BoxConstraints(),
                ),
              ],
            ),
            const SizedBox(height: 6),

            // Tab Bar: EXA vs DUMPING PIN
            Container(
              height: 34,
              decoration: BoxDecoration(
                color: FmsTheme.cardBg,
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: FmsTheme.cardBorder),
              ),
              child: TabBar(
                controller: _tabController,
                indicator: BoxDecoration(
                  color: const Color(0xFF0D2544),
                  borderRadius: BorderRadius.circular(6),
                  border: Border.all(color: FmsTheme.cyanAccent),
                ),
                labelColor: FmsTheme.cyanAccent,
                unselectedLabelColor: FmsTheme.textMuted,
                labelStyle: const TextStyle(
                  fontWeight: FontWeight.bold,
                  fontSize: 11,
                ),
                tabs: const [
                  Tab(text: "🚜  EXCAVATOR / SHOVEL (FRONT GALI)"),
                  Tab(text: "📍  DUMPING PIN (DISPOSAL / STOCKPILE)"),
                ],
              ),
            ),
            const SizedBox(height: 8),

            // Tab Content
            Expanded(
              child: TabBarView(
                controller: _tabController,
                children: [
                  // 1. Excavators List
                  _buildExaList(api, excavators),

                  // 2. Dumping Pins List
                  _buildDumpingList(api, dumpingPins),
                ],
              ),
            ),

            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  "Target Aktif: ${api.activeTargetName} (${api.activeTargetType})",
                  style: FmsTheme.caption.copyWith(
                    color: FmsTheme.emeraldGreen,
                  ),
                ),
                Row(
                  children: [
                    TextButton(
                      onPressed: () {
                        api.clearManualTarget();
                        Navigator.of(context).pop();
                      },
                      child: const Text('Ikuti assignment'),
                    ),
                    TextButton(
                      onPressed: () => Navigator.of(context).pop(),
                      child: Text(
                        "TUTUP",
                        style: FmsTheme.titleMedium.copyWith(
                          color: FmsTheme.textMuted,
                          fontSize: 11,
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildExaList(FmsApiService api, List excavators) {
    if (excavators.isEmpty) {
      return Center(
        child: Text(
          "Tidak ada data Excavator di server.",
          style: FmsTheme.bodyNormal,
        ),
      );
    }

    return ListView.separated(
      physics: const BouncingScrollPhysics(),
      itemCount: excavators.length,
      separatorBuilder: (context, index) => const SizedBox(height: 6),
      itemBuilder: (context, idx) {
        final exa = excavators[idx];
        final isSelected =
            exa.unitName.toUpperCase() == api.activeTargetName.toUpperCase();

        return InkWell(
          onTap: () {
            api.setManualTarget(
              exa.unitName,
              "Shovel (${exa.unitType})",
              exa.easting,
              exa.northing,
            );
            Navigator.of(context).pop();
          },
          borderRadius: BorderRadius.circular(8),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            decoration: BoxDecoration(
              color: isSelected ? const Color(0xE0062816) : FmsTheme.cardBg,
              borderRadius: BorderRadius.circular(8),
              border: Border.all(
                color: isSelected ? FmsTheme.emeraldGreen : FmsTheme.cardBorder,
                width: isSelected ? 1.6 : 1.0,
              ),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    Text(
                      isSelected ? "🚜 ● " : "🚜   ",
                      style: TextStyle(
                        color: isSelected
                            ? FmsTheme.emeraldGreen
                            : Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 13,
                      ),
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          exa.unitName,
                          style: FmsTheme.titleMedium.copyWith(
                            color: isSelected
                                ? FmsTheme.emeraldGreen
                                : Colors.white,
                            fontSize: 12,
                          ),
                        ),
                        Text(
                          "${exa.unitType} | Act: ${exa.activityName}",
                          style: FmsTheme.caption.copyWith(fontSize: 10),
                        ),
                      ],
                    ),
                  ],
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(
                      "UTM: ${exa.easting.toStringAsFixed(0)}, ${exa.northing.toStringAsFixed(0)}",
                      style: FmsTheme.codePill.copyWith(fontSize: 9),
                    ),
                    Text(
                      exa.hasFreshGps ? "GPS AKTIF" : "GPS LAMA",
                      style: FmsTheme.caption.copyWith(
                        color: exa.hasFreshGps
                            ? FmsTheme.emeraldGreen
                            : FmsTheme.amberWarning,
                        fontWeight: FontWeight.bold,
                        fontSize: 9,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildDumpingList(FmsApiService api, List dumpingPins) {
    if (dumpingPins.isEmpty) {
      return Center(
        child: Text(
          'Lokasi disposal aktual belum tersedia.',
          style: FmsTheme.bodyNormal,
        ),
      );
    }
    return ListView.separated(
      physics: const BouncingScrollPhysics(),
      itemCount: dumpingPins.length,
      separatorBuilder: (context, index) => const SizedBox(height: 6),
      itemBuilder: (context, idx) {
        final loc = dumpingPins[idx];
        final isSelected =
            loc.name.toUpperCase() == api.activeTargetName.toUpperCase();

        return InkWell(
          onTap: () {
            api.setManualTarget(
              loc.name,
              "Dumping Pin (${loc.type})",
              loc.easting,
              loc.northing,
            );
            Navigator.of(context).pop();
          },
          borderRadius: BorderRadius.circular(8),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            decoration: BoxDecoration(
              color: isSelected ? const Color(0xE0062816) : FmsTheme.cardBg,
              borderRadius: BorderRadius.circular(8),
              border: Border.all(
                color: isSelected ? FmsTheme.emeraldGreen : FmsTheme.cardBorder,
                width: isSelected ? 1.6 : 1.0,
              ),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    Text(
                      isSelected ? "📍 ● " : "📍   ",
                      style: TextStyle(
                        color: isSelected
                            ? FmsTheme.emeraldGreen
                            : Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 13,
                      ),
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          loc.name,
                          style: FmsTheme.titleMedium.copyWith(
                            color: isSelected
                                ? FmsTheme.emeraldGreen
                                : Colors.white,
                            fontSize: 12,
                          ),
                        ),
                        Text(
                          "Kategori: ${loc.category} | Tipe: ${loc.type}",
                          style: FmsTheme.caption.copyWith(fontSize: 10),
                        ),
                      ],
                    ),
                  ],
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(
                      "UTM: ${loc.easting.toStringAsFixed(0)}, ${loc.northing.toStringAsFixed(0)}",
                      style: FmsTheme.codePill.copyWith(fontSize: 9),
                    ),
                    Text(
                      "Elev: ${loc.elevation.toStringAsFixed(0)}m",
                      style: FmsTheme.caption.copyWith(
                        color: FmsTheme.cyanAccent,
                        fontSize: 9,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

import 'package:flutter/material.dart';

import '../theme/fms_theme.dart';
import '../services/fms_api_service.dart';

class ServerSettingsDialog extends StatefulWidget {
  const ServerSettingsDialog({super.key});

  @override
  State<ServerSettingsDialog> createState() => _ServerSettingsDialogState();
}

class _ServerSettingsDialogState extends State<ServerSettingsDialog> {
  late TextEditingController _urlCtrl;
  final FmsApiService _api = FmsApiService();

  @override
  void initState() {
    super.initState();
    _urlCtrl = TextEditingController(text: _api.backendBaseUrl);
  }

  @override
  void dispose() {
    _urlCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Dialog(
      backgroundColor: Colors.transparent,
      child: Container(
        width: 520,
        padding: const EdgeInsets.all(18),
        decoration: BoxDecoration(
          color: FmsTheme.surfaceDark,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: FmsTheme.cyanAccent, width: 1.5),
          boxShadow: FmsTheme.neonGlowShadow(FmsTheme.cyanAccent, opacity: 0.3),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Text("⚙️ ", style: TextStyle(fontSize: 20)),
                Text(
                  "KONFIGURASI API FMS",
                  style: FmsTheme.titleMedium.copyWith(
                    color: FmsTheme.cyanAccent,
                  ),
                ),
              ],
            ),
            const Divider(color: FmsTheme.cardBorder),
            const SizedBox(height: 12),

            Text("Alamat API FMS", style: FmsTheme.caption),
            Text(
              'Gunakan HTTPS untuk domain publik, atau HTTP untuk IP lokal tambang.',
              style: FmsTheme.caption,
            ),
            const SizedBox(height: 6),
            TextField(
              controller: _urlCtrl,
              style: FmsTheme.codePill.copyWith(color: Colors.white),
              decoration: InputDecoration(
                filled: true,
                fillColor: FmsTheme.cardBg,
                hintText: "http://172.16.1.92:8000",
                hintStyle: FmsTheme.caption,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: const BorderSide(color: FmsTheme.cardBorder),
                ),
                enabledBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: const BorderSide(color: FmsTheme.cardBorder),
                ),
                focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: const BorderSide(
                    color: FmsTheme.emeraldGreen,
                    width: 1.5,
                  ),
                ),
              ),
            ),
            const SizedBox(height: 12),

            Row(
              children: [
                Text("Status Koneksi: ", style: FmsTheme.caption),
                Text(
                  _api.isApiConnected ? "● ONLINE" : "● OFFLINE",
                  style: FmsTheme.titleMedium.copyWith(
                    color: _api.isApiConnected
                        ? FmsTheme.emeraldGreen
                        : FmsTheme.redHazard,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 18),

            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton(
                  onPressed: () => Navigator.of(context).pop(),
                  child: Text(
                    "BATAL",
                    style: FmsTheme.titleMedium.copyWith(
                      color: FmsTheme.textMuted,
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                ElevatedButton(
                  onPressed: () {
                    _api.updateBackendUrl(_urlCtrl.text);
                    Navigator.of(context).pop();
                  },
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF036B39),
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                  child: const Text("SIMPAN & HUBUNGKAN"),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

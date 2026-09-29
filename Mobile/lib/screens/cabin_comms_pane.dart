import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../services/fms_api_service.dart';
import '../services/live_cabin_comms_service.dart';
import '../theme/fms_theme.dart';

class CabinCommsPane extends StatefulWidget {
  const CabinCommsPane({super.key});

  @override
  State<CabinCommsPane> createState() => _CabinCommsPaneState();
}

class _CabinCommsPaneState extends State<CabinCommsPane> {
  final api = FmsApiService();
  final tokenInput = TextEditingController();
  final messageInput = TextEditingController();
  final live = LiveCabinCommsService();
  Timer? poll;
  String? unitKey;
  String? boundUnit;
  String status = 'Memeriksa layanan pesan...';
  bool serviceEnabled = false;
  bool busy = false;
  List<Map<String, dynamic>> messages = [];

  Uri endpoint(String path, [Map<String, String>? query]) => Uri.parse(
    '${api.backendBaseUrl.replaceAll(RegExp(r'/+$'), '')}/api/v1/comms/$path',
  ).replace(queryParameters: query);

  @override
  void initState() {
    super.initState();
    unitKey = api.cabinCommsKeys[api.selectedUnitId];
    if (unitKey != null) boundUnit = api.selectedUnitId;
    live.addListener(_onLiveChanged);
    checkStatus();
    poll = Timer.periodic(const Duration(seconds: 5), (_) {
      if (!serviceEnabled) checkStatus();
      refresh();
    });
    if (unitKey != null) {
      live.connect(api.backendBaseUrl, boundUnit!, unitKey!);
      refresh();
    }
  }

  void _onLiveChanged() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    poll?.cancel();
    tokenInput.dispose();
    messageInput.dispose();
    live.removeListener(_onLiveChanged);
    super.dispose();
  }

  Future<void> checkStatus() async {
    try {
      final response = await http
          .get(endpoint('status'))
          .timeout(const Duration(seconds: 8));
      final data = jsonDecode(response.body) as Map<String, dynamic>;
      if (!mounted) return;
      setState(() {
        serviceEnabled = response.statusCode == 200 && data['enabled'] == true;
        status = serviceEnabled
            ? unitKey == null
                  ? 'Masukkan kode pasangan untuk ${api.selectedUnitId}.'
                  : 'Menghubungkan pesan untuk ${api.selectedUnitId}...'
            : 'Layanan pesan belum diaktifkan pada server.';
      });
    } catch (_) {
      if (mounted) {
        setState(
          () => status = 'Layanan pesan belum tersedia pada server ini.',
        );
      }
    }
  }

  Future<void> connect() async {
    final key = tokenInput.text.trim();
    final unit = api.selectedUnitId;
    if (key.isEmpty || unit.isEmpty) return;
    setState(() => busy = true);
    try {
      final response = await http
          .get(
            endpoint('messages', {'unit_name': unit}),
            headers: {'X-FMS-Unit-Key': key},
          )
          .timeout(const Duration(seconds: 8));
      if (response.statusCode != 200) {
        throw Exception('Kode pasangan tidak valid (${response.statusCode}).');
      }
      if (!mounted) return;
      setState(() {
        unitKey = key;
        boundUnit = unit;
        api.cabinCommsKeys[unit] = key;
        tokenInput.clear();
        status = 'Terhubung ke ruang kontrol';
        messages = (jsonDecode(response.body)['data'] as List)
            .map((item) => Map<String, dynamic>.from(item as Map))
            .toList();
      });
      await live.connect(api.backendBaseUrl, unit, key);
    } catch (error) {
      if (mounted) {
        setState(
          () => status = error.toString().replaceFirst('Exception: ', ''),
        );
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> refresh() async {
    final key = unitKey;
    final unit = boundUnit;
    if (!mounted || key == null || unit == null) return;
    if (api.selectedUnitId != unit) {
      setState(() {
        live.disconnect();
        unitKey = null;
        boundUnit = null;
        unitKey = api.cabinCommsKeys[api.selectedUnitId];
        if (unitKey != null) boundUnit = api.selectedUnitId;
        messages = [];
        status = unitKey == null
            ? 'Unit berubah. Masukkan kode pasangan unit baru.'
            : 'Menghubungkan pesan untuk ${api.selectedUnitId}...';
      });
      if (unitKey != null) {
        live.connect(api.backendBaseUrl, boundUnit!, unitKey!);
      }
      return;
    }
    try {
      final response = await http
          .get(
            endpoint('messages', {'unit_name': unit}),
            headers: {'X-FMS-Unit-Key': key},
          )
          .timeout(const Duration(seconds: 8));
      if (!mounted) return;
      if (response.statusCode == 403) {
        setState(() {
          live.disconnect();
          unitKey = null;
          boundUnit = null;
          api.cabinCommsKeys.remove(unit);
          messages = [];
          status = 'Kode pasangan dicabut. Hubungkan ulang.';
        });
      } else if (response.statusCode == 200) {
        setState(() {
          messages = (jsonDecode(response.body)['data'] as List)
              .map((item) => Map<String, dynamic>.from(item as Map))
              .toList();
          status = 'Terhubung ke ruang kontrol';
        });
      } else {
        setState(
          () => status = 'Sinkronisasi pesan gagal (${response.statusCode}).',
        );
      }
    } catch (_) {
      if (mounted) {
        setState(() => status = 'Koneksi pesan terputus. Mencoba kembali...');
      }
    }
  }

  Future<void> send() async {
    final key = unitKey;
    final unit = boundUnit;
    final body = messageInput.text.trim();
    if (key == null || unit == null || body.isEmpty || busy) return;
    setState(() => busy = true);
    try {
      final response = await http
          .post(
            endpoint('messages'),
            headers: {
              'X-FMS-Unit-Key': key,
              'Content-Type': 'application/json',
            },
            body: jsonEncode({
              'unit_name': unit,
              'body': body,
              'priority': 'normal',
            }),
          )
          .timeout(const Duration(seconds: 10));
      if (response.statusCode != 200) {
        throw Exception('Pesan gagal terkirim (${response.statusCode}).');
      }
      messageInput.clear();
      await refresh();
    } catch (error) {
      if (mounted) {
        setState(
          () => status = error.toString().replaceFirst('Exception: ', ''),
        );
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'Pesan kabin / ${api.selectedUnitId}',
          style: FmsTheme.titleMedium,
        ),
        const SizedBox(height: 5),
        Text(
          unitKey == null ? status : '${live.status}  |  $status',
          style: FmsTheme.caption.copyWith(
            color: serviceEnabled && unitKey != null
                ? FmsTheme.emeraldGreen
                : FmsTheme.amberWarning,
          ),
        ),
        const SizedBox(height: 12),
        if (!serviceEnabled)
          Expanded(
            child: Center(
              child: OutlinedButton.icon(
                onPressed: checkStatus,
                icon: const Icon(Icons.refresh),
                label: const Text('Coba lagi'),
              ),
            ),
          )
        else if (unitKey == null) ...[
          TextField(
            controller: tokenInput,
            obscureText: true,
            enabled: serviceEnabled,
            decoration: const InputDecoration(
              labelText: 'Kode pasangan unit',
              prefixIcon: Icon(Icons.key_outlined),
            ),
          ),
          const SizedBox(height: 8),
          FilledButton.icon(
            onPressed: serviceEnabled && !busy ? connect : null,
            icon: const Icon(Icons.link),
            label: const Text('Hubungkan kabin'),
          ),
        ] else ...[
          Expanded(
            child: messages.isEmpty
                ? Center(
                    child: Text('Belum ada pesan.', style: FmsTheme.caption),
                  )
                : ListView.builder(
                    itemCount: messages.length,
                    itemBuilder: (context, index) {
                      final item = messages[index];
                      final fromDispatch = item['sender_role'] == 'dispatcher';
                      final voice = item['kind'] == 'voice';
                      return Container(
                        margin: const EdgeInsets.only(bottom: 6),
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: fromDispatch
                              ? FmsTheme.cardHeaderBg
                              : FmsTheme.bgDark,
                          border: Border.all(color: FmsTheme.cardBorder),
                          borderRadius: BorderRadius.circular(4),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              fromDispatch ? 'RUANG KONTROL' : 'KABIN',
                              style: FmsTheme.caption.copyWith(
                                color: FmsTheme.emeraldGreen,
                              ),
                            ),
                            const SizedBox(height: 4),
                            voice
                                ? Text(
                                    'Pesan suara lama',
                                    style: FmsTheme.caption,
                                  )
                                : Text(item['body']?.toString() ?? ''),
                            Text(
                              item['sent_at']
                                      ?.toString()
                                      .replaceFirst('T', ' ')
                                      .split('.')
                                      .first ??
                                  '',
                              style: FmsTheme.caption,
                            ),
                          ],
                        ),
                      );
                    },
                  ),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: messageInput,
                  maxLength: 500,
                  onSubmitted: (_) => send(),
                  decoration: const InputDecoration(
                    hintText: 'Pesan untuk ruang kontrol',
                    counterText: '',
                  ),
                ),
              ),
              IconButton(
                tooltip: 'Kirim pesan',
                onPressed: busy ? null : send,
                icon: const Icon(Icons.send, color: FmsTheme.emeraldGreen),
              ),
            ],
          ),
          const SizedBox(height: 6),
          SizedBox(
            height: 42,
            child: FilledButton.icon(
              onPressed: !live.connected
                  ? null
                  : live.speaking || live.requestingMic
                  ? live.stopSpeaking
                  : live.startSpeaking,
              style: FilledButton.styleFrom(
                backgroundColor: live.speaking || live.requestingMic
                    ? FmsTheme.redHazard
                    : FmsTheme.emeraldGreen,
                foregroundColor: FmsTheme.bgDark,
              ),
              icon: Icon(
                live.speaking || live.requestingMic ? Icons.stop : Icons.mic,
              ),
              label: Text(
                live.speaking
                    ? 'Akhiri bicara langsung'
                    : live.requestingMic
                    ? 'Membuka kanal radio...'
                    : 'Bicara langsung ke kontrol',
              ),
            ),
          ),
        ],
      ],
    );
  }
}

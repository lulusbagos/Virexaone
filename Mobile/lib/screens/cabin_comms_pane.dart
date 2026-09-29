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

class _CabinCommsPaneState extends State<CabinCommsPane>
    with SingleTickerProviderStateMixin {
  final api = FmsApiService();
  final tokenInput = TextEditingController();
  final messageInput = TextEditingController();
  final scrollController = ScrollController();
  final live = LiveCabinCommsService();
  late AnimationController _pulseController;
  Timer? poll;
  String? unitKey;
  String? boundUnit;
  String status = 'Memeriksa layanan komunikasi...';
  bool serviceEnabled = false;
  bool busy = false;
  List<Map<String, dynamic>> messages = [];

  final List<String> quickPresets = [
    'Siap Muat',
    'Menuju Front Gali',
    'Antre Disposal',
    'Selesai Dumping',
    'Perlu Fuel / Refuel',
    'Kendala Jalan / Breakdown',
    'Minta Arahan Dispatcher',
  ];

  Uri endpoint(String path, [Map<String, String>? query]) => Uri.parse(
    '/api/v1/comms/',
  ).replace(queryParameters: query);

  @override
  void initState() {
    super.initState();
    _pulseController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1200),
    )..repeat(reverse: true);

    unitKey = api.cabinCommsKeys[api.selectedUnitId];
    if (unitKey != null) boundUnit = api.selectedUnitId;
    live.addListener(_onLiveChanged);
    checkStatus();
    poll = Timer.periodic(const Duration(seconds: 4), (_) {
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
    _pulseController.dispose();
    tokenInput.dispose();
    messageInput.dispose();
    scrollController.dispose();
    live.removeListener(_onLiveChanged);
    super.dispose();
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (scrollController.hasClients) {
        scrollController.animateTo(
          scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeOut,
        );
      }
    });
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
                ? 'Masukkan kode pasangan untuk .'
                : 'Menghubungkan pesan untuk ...'
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
        throw Exception('Kode pasangan tidak valid ().');
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
      _scrollToBottom();
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
            : 'Menghubungkan pesan untuk ...';
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
        final newMsgs = (jsonDecode(response.body)['data'] as List)
            .map((item) => Map<String, dynamic>.from(item as Map))
            .toList();
        final hadMore = newMsgs.length > messages.length;
        setState(() {
          messages = newMsgs;
          status = 'Terhubung ke ruang kontrol';
        });
        if (hadMore) _scrollToBottom();
      } else {
        setState(
          () => status = 'Sinkronisasi pesan gagal ().',
        );
      }
    } catch (_) {
      if (mounted) {
        setState(() => status = 'Koneksi pesan terputus. Mencoba kembali...');
      }
    }
  }

  Future<void> send([String? textOverride]) async {
    final key = unitKey;
    final unit = boundUnit;
    final body = (textOverride ?? messageInput.text).trim();
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
        throw Exception('Pesan gagal terkirim ().');
      }
      if (textOverride == null) messageInput.clear();
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
        // Top Header Info
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: FmsTheme.cardHeaderBg,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(
              color: live.connected
                  ? FmsTheme.emeraldGreen.withValues(alpha: 0.5)
                  : FmsTheme.amberWarning.withValues(alpha: 0.4),
            ),
          ),
          child: Row(
            children: [
              Icon(
                live.connected ? Icons.radio : Icons.radio_button_off,
                size: 16,
                color: live.connected
                    ? FmsTheme.emeraldGreen
                    : FmsTheme.amberWarning,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  unitKey == null ? status : '  |  ',
                  style: FmsTheme.caption.copyWith(
                    color: serviceEnabled && unitKey != null
                        ? FmsTheme.emeraldGreen
                        : FmsTheme.amberWarning,
                    fontWeight: FontWeight.w600,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 8),

        // Body Content
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
          Expanded(
            child: Center(
              child: Container(
                constraints: const BoxConstraints(maxWidth: 380),
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: FmsTheme.cardBg,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: FmsTheme.cardBorder),
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(
                      Icons.phonelink_ring_outlined,
                      size: 36,
                      color: FmsTheme.cyanAccent,
                    ),
                    const SizedBox(height: 10),
                    Text(
                      'Hubungkan Kabin ',
                      style: FmsTheme.titleMedium,
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'Masukkan kode pasangan dari kontrol untuk mengaktifkan radio dan pesan.',
                      textAlign: TextAlign.center,
                      style: FmsTheme.caption,
                    ),
                    const SizedBox(height: 14),
                    TextField(
                      controller: tokenInput,
                      obscureText: true,
                      enabled: serviceEnabled,
                      style: FmsTheme.bodyNormal,
                      decoration: const InputDecoration(
                        labelText: 'Kode Pasangan Unit',
                        prefixIcon: Icon(Icons.key_outlined, size: 18),
                        isDense: true,
                      ),
                    ),
                    const SizedBox(height: 12),
                    SizedBox(
                      width: double.infinity,
                      height: 42,
                      child: FilledButton.icon(
                        onPressed: serviceEnabled && !busy ? connect : null,
                        icon: const Icon(Icons.link, size: 18),
                        label: const Text('Hubungkan Sekarang'),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ] else ...[
          // Messages List Viewport
          Expanded(
            child: messages.isEmpty
                ? Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.chat_bubble_outline,
                          size: 32,
                          color: FmsTheme.textMuted.withValues(alpha: 0.5),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'Belum ada pesan komunikasi.',
                          style: FmsTheme.caption,
                        ),
                      ],
                    ),
                  )
                : ListView.builder(
                    controller: scrollController,
                    padding: const EdgeInsets.symmetric(vertical: 4),
                    itemCount: messages.length,
                    itemBuilder: (context, index) {
                      final item = messages[index];
                      final fromDispatch = item['sender_role'] == 'dispatcher';
                      final voice = item['kind'] == 'voice';
                      final priority = item['priority']?.toString();
                      final isUrgent = priority == 'urgent';

                      return Align(
                        alignment: fromDispatch
                            ? Alignment.centerLeft
                            : Alignment.centerRight,
                        child: Container(
                          constraints: BoxConstraints(
                            maxWidth: MediaQuery.sizeOf(context).width * 0.76,
                          ),
                          margin: const EdgeInsets.only(bottom: 8),
                          padding: const EdgeInsets.all(10),
                          decoration: BoxDecoration(
                            gradient: fromDispatch
                                ? const LinearGradient(
                                    colors: [Color(0xF00D2235), Color(0xF0071322)],
                                  )
                                : const LinearGradient(
                                    colors: [Color(0xF00B2E21), Color(0xF0051711)],
                                  ),
                            border: Border.all(
                              color: isUrgent
                                  ? FmsTheme.redHazard
                                  : fromDispatch
                                      ? FmsTheme.cyanAccent.withValues(alpha: 0.6)
                                      : FmsTheme.emeraldGreen.withValues(alpha: 0.6),
                              width: 1.2,
                            ),
                            borderRadius: BorderRadius.only(
                              topLeft: const Radius.circular(10),
                              topRight: const Radius.circular(10),
                              bottomLeft: Radius.circular(fromDispatch ? 2 : 10),
                              bottomRight: Radius.circular(fromDispatch ? 10 : 2),
                            ),
                            boxShadow: isUrgent
                                ? FmsTheme.neonGlowShadow(FmsTheme.redHazard, opacity: 0.3)
                                : null,
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Icon(
                                    fromDispatch
                                        ? Icons.support_agent
                                        : Icons.local_shipping,
                                    size: 13,
                                    color: fromDispatch
                                        ? FmsTheme.cyanAccent
                                        : FmsTheme.emeraldGreen,
                                  ),
                                  const SizedBox(width: 4),
                                  Text(
                                    fromDispatch ? 'RUANG KONTROL' : 'KABIN',
                                    style: FmsTheme.caption.copyWith(
                                      color: fromDispatch
                                          ? FmsTheme.cyanAccent
                                          : FmsTheme.emeraldGreen,
                                      fontWeight: FontWeight.w700,
                                      fontSize: 9.5,
                                    ),
                                  ),
                                  if (isUrgent) ...[
                                    const SizedBox(width: 6),
                                    Container(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 4,
                                        vertical: 1,
                                      ),
                                      decoration: BoxDecoration(
                                        color: FmsTheme.redHazard,
                                        borderRadius: BorderRadius.circular(3),
                                      ),
                                      child: const Text(
                                        'URGENT',
                                        style: TextStyle(
                                          color: Colors.white,
                                          fontSize: 7.5,
                                          fontWeight: FontWeight.w900,
                                        ),
                                      ),
                                    ),
                                  ],
                                ],
                              ),
                              const SizedBox(height: 4),
                              voice
                                  ? Row(
                                      children: [
                                        const Icon(Icons.multitrack_audio, size: 16),
                                        const SizedBox(width: 6),
                                        Text('Transmisi Suara Live', style: FmsTheme.bodyNormal),
                                      ],
                                    )
                                  : Text(
                                      item['body']?.toString() ?? '',
                                      style: FmsTheme.bodyNormal.copyWith(
                                        fontSize: 12.5,
                                        color: Colors.white,
                                      ),
                                    ),
                              const SizedBox(height: 3),
                              Align(
                                alignment: Alignment.bottomRight,
                                child: Text(
                                  item['sent_at']
                                          ?.toString()
                                          .replaceFirst('T', ' ')
                                          .split('.')
                                          .first ??
                                      '',
                                  style: FmsTheme.caption.copyWith(
                                    fontSize: 8.5,
                                    color: FmsTheme.textMuted,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      );
                    },
                  ),
          ),

          // Quick Presets Bar
          SizedBox(
            height: 32,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              itemCount: quickPresets.length,
              separatorBuilder: (context, index) => const SizedBox(width: 6),
              itemBuilder: (context, idx) {
                final preset = quickPresets[idx];
                return ActionChip(
                  visualDensity: VisualDensity.compact,
                  backgroundColor: const Color(0xFF091C2E),
                  side: BorderSide(color: FmsTheme.cyanAccent.withValues(alpha: 0.35)),
                  label: Text(
                    preset,
                    style: const TextStyle(fontSize: 10, color: FmsTheme.cyanAccent),
                  ),
                  onPressed: busy ? null : () => send(preset),
                );
              },
            ),
          ),
          const SizedBox(height: 6),

          // Text Message Input Bar
          Row(
            children: [
              Expanded(
                child: SizedBox(
                  height: 44,
                  child: TextField(
                    controller: messageInput,
                    maxLength: 500,
                    onSubmitted: (_) => send(),
                    style: FmsTheme.bodyNormal,
                    decoration: InputDecoration(
                      hintText: 'Ketik pesan ke kontrol...',
                      hintStyle: FmsTheme.caption,
                      counterText: '',
                      filled: true,
                      fillColor: const Color(0xFF081728),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(8),
                        borderSide: const BorderSide(color: FmsTheme.cardBorder),
                      ),
                      contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 6),
              SizedBox(
                width: 44,
                height: 44,
                child: IconButton.filled(
                  tooltip: 'Kirim pesan',
                  onPressed: busy ? null : () => send(),
                  style: IconButton.styleFrom(
                    backgroundColor: FmsTheme.cyanAccent,
                    foregroundColor: FmsTheme.bgDark,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                  ),
                  icon: const Icon(Icons.send_rounded, size: 18),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),

          // PTT (Push-to-Talk) Talkback Button with Pulsing Animation
          AnimatedBuilder(
            animation: _pulseController,
            builder: (context, child) {
              final isLive = live.speaking || live.requestingMic;
              final pulseGlow = isLive
                  ? FmsTheme.neonGlowShadow(
                      FmsTheme.redHazard,
                      opacity: 0.3 + 0.35 * _pulseController.value,
                      blur: 14 + 6 * _pulseController.value,
                    )
                  : null;

              return Container(
                height: 48,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(10),
                  boxShadow: pulseGlow,
                ),
                child: FilledButton.icon(
                  onPressed: !live.connected
                      ? null
                      : isLive
                          ? live.stopSpeaking
                          : live.startSpeaking,
                  style: FilledButton.styleFrom(
                    backgroundColor: isLive
                        ? FmsTheme.redHazard
                        : FmsTheme.emeraldGreen,
                    foregroundColor: FmsTheme.bgDark,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                  ),
                  icon: Icon(
                    isLive ? Icons.stop_circle : Icons.mic,
                    size: 22,
                  ),
                  label: Text(
                    live.speaking
                        ? 'SEDANG BICARA (TEKAN UNTUK SELESAI)'
                        : live.requestingMic
                            ? 'MEMBUKA KANAL RADIO LIVE...'
                            : 'TEKAN UNTUK BICARA LANGSUNG KE KONTROL',
                    style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 11,
                      letterSpacing: 0.5,
                    ),
                  ),
                ),
              );
            },
          ),
        ],
      ],
    );
  }
}

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
  final messageInput = TextEditingController();
  final scrollController = ScrollController();
  final live = LiveCabinCommsService();
  late AnimationController _pulseController;
  Timer? poll;
  String? unitKey;
  String? boundUnit;
  String status = 'Menghubungkan otomatis ke Ruang Kontrol...';
  bool serviceEnabled = false;
  bool busy = false;
  bool isAutoPairing = false;
  List<Map<String, dynamic>> messages = [];

  final List<Map<String, dynamic>> quickPresets = [
    {'icon': '🚜', 'label': 'SIAP MUAT', 'text': 'Unit siap muat di Front'},
    {'icon': '🚛', 'label': 'MENUJU FRONT', 'text': 'Sedang traveling menuju Front Gali'},
    {'icon': '🛑', 'label': 'ANTRE DISPOSAL', 'text': 'Antre dumping di Disposal'},
    {'icon': '📦', 'label': 'SELESAI DUMP', 'text': 'Selesai dumping, kembali ke front'},
    {'icon': '⛽', 'label': 'PERLU FUEL', 'text': 'Bahan bakar menipis, butuh fuel truck'},
    {'icon': '⚠️', 'label': 'KENDALA JALAN', 'text': 'Ada hazard / kendala di jalur hauling'},
    {'icon': '📢', 'label': 'MINTA ASSIGNMENT', 'text': 'Mohon konfirmasi assignment baru'},
  ];

  Uri endpoint(String path, [Map<String, String>? query]) => Uri.parse(
    '${api.backendBaseUrl.replaceAll(RegExp(r'/+$'), '')}/api/v1/comms/$path',
  ).replace(queryParameters: query);

  @override
  void initState() {
    super.initState();
    _pulseController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1000),
    )..repeat(reverse: true);

    live.addListener(_onLiveChanged);
    _initAutoConnection();

    poll = Timer.periodic(const Duration(seconds: 3), (_) {
      if (!serviceEnabled || unitKey == null) {
        _initAutoConnection();
      } else {
        refresh();
      }
    });
  }

  void _onLiveChanged() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    poll?.cancel();
    _pulseController.dispose();
    messageInput.dispose();
    scrollController.dispose();
    live.removeListener(_onLiveChanged);
    super.dispose();
  }

  Future<void> _initAutoConnection() async {
    final unit = api.selectedUnitId.isEmpty ? 'RD5100' : api.selectedUnitId;
    if (isAutoPairing) return;
    isAutoPairing = true;
    try {
      final key = await api.ensureCabinCommsPairing(unit);
      if (!mounted) return;
      if (key != null && key.isNotEmpty) {
        setState(() {
          unitKey = key;
          boundUnit = unit;
          serviceEnabled = true;
          status = 'Radio & Pesan Terhubung';
        });
        await refresh();
      } else {
        setState(() {
          serviceEnabled = false;
          status = 'Mencoba menghubungkan ke server FMS...';
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() => status = 'Menghubungkan ulang...');
      }
    } finally {
      isAutoPairing = false;
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (scrollController.hasClients) {
        scrollController.animateTo(
          scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 200),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> refresh() async {
    final key = unitKey;
    final unit = boundUnit ?? api.selectedUnitId;
    if (!mounted || key == null || unit.isEmpty) return;

    try {
      final response = await http
          .get(
            endpoint('messages', {'unit_name': unit}),
            headers: {'X-FMS-Unit-Key': key},
          )
          .timeout(const Duration(seconds: 6));
      if (!mounted) return;
      if (response.statusCode == 200) {
        final newMsgs = (jsonDecode(response.body)['data'] as List)
            .map((item) => Map<String, dynamic>.from(item as Map))
            .toList();
        final hadMore = newMsgs.length > messages.length;
        setState(() {
          messages = newMsgs;
          serviceEnabled = true;
          status = 'Terhubung ke Ruang Kontrol';
        });
        if (hadMore) _scrollToBottom();
      }
    } catch (_) {}
  }

  Future<void> send([String? textOverride]) async {
    final key = unitKey;
    final unit = boundUnit ?? api.selectedUnitId;
    final body = (textOverride ?? messageInput.text).trim();
    if (key == null || unit.isEmpty || body.isEmpty || busy) return;
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
          .timeout(const Duration(seconds: 8));
      if (response.statusCode == 200) {
        if (textOverride == null) messageInput.clear();
        await refresh();
      }
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
    final activeUnit = api.selectedUnitId.isEmpty ? 'KABIN' : api.selectedUnitId;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // 1. Live Radio & Comms Status Banner
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          decoration: BoxDecoration(
            color: FmsTheme.cardHeaderBg,
            borderRadius: BorderRadius.circular(10),
            border: Border.all(
              color: live.connected
                  ? FmsTheme.emeraldGreen.withValues(alpha: 0.7)
                  : FmsTheme.amberWarning.withValues(alpha: 0.6),
              width: 1.2,
            ),
            boxShadow: live.connected
                ? FmsTheme.neonGlowShadow(FmsTheme.emeraldGreen, opacity: 0.25)
                : null,
          ),
          child: Row(
            children: [
              Container(
                width: 10,
                height: 10,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: live.connected
                      ? FmsTheme.emeraldGreen
                      : FmsTheme.amberWarning,
                  boxShadow: FmsTheme.neonGlowShadow(
                    live.connected
                        ? FmsTheme.emeraldGreen
                        : FmsTheme.amberWarning,
                    opacity: 0.8,
                    blur: 6,
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'KOMUNIKASI KABIN / $activeUnit',
                      style: FmsTheme.titleMedium.copyWith(
                        color: FmsTheme.emeraldGreen,
                        fontSize: 12,
                        letterSpacing: 0.5,
                      ),
                    ),
                    Text(
                      live.connected
                          ? 'Radio PTT & Pesan Terhubung Langsung'
                          : status,
                      style: FmsTheme.caption.copyWith(
                        color: live.connected
                            ? FmsTheme.textLight
                            : FmsTheme.amberWarning,
                        fontSize: 10,
                      ),
                    ),
                  ],
                ),
              ),
              IconButton(
                tooltip: 'Segarkan',
                icon: const Icon(Icons.refresh, size: 18, color: FmsTheme.cyanAccent),
                onPressed: () {
                  _initAutoConnection();
                  refresh();
                },
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
              ),
            ],
          ),
        ),
        const SizedBox(height: 8),

        // 2. Main Messages Thread Viewport (High-Contrast & Operator Friendly)
        Expanded(
          child: messages.isEmpty
              ? Center(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(
                        Icons.speaker_notes_outlined,
                        size: 40,
                        color: FmsTheme.cyanAccent.withValues(alpha: 0.4),
                      ),
                      const SizedBox(height: 10),
                      Text(
                        'Belum ada pesan masuk.',
                        style: FmsTheme.titleMedium.copyWith(
                          color: FmsTheme.textLight,
                          fontSize: 14,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Pesan dari kontrol atau kiriman kabin akan tampil di sini.',
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
                          maxWidth: MediaQuery.sizeOf(context).width * 0.82,
                        ),
                        margin: const EdgeInsets.only(bottom: 10),
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          gradient: fromDispatch
                              ? const LinearGradient(
                                  colors: [Color(0xF50D243A), Color(0xF5071424)],
                                )
                              : const LinearGradient(
                                  colors: [Color(0xF50A3324), Color(0xF5041A12)],
                                ),
                          border: Border.all(
                            color: isUrgent
                                ? FmsTheme.redHazard
                                : fromDispatch
                                    ? FmsTheme.cyanAccent.withValues(alpha: 0.75)
                                    : FmsTheme.emeraldGreen.withValues(alpha: 0.75),
                            width: isUrgent ? 2.0 : 1.2,
                          ),
                          borderRadius: BorderRadius.only(
                            topLeft: const Radius.circular(12),
                            topRight: const Radius.circular(12),
                            bottomLeft: Radius.circular(fromDispatch ? 2 : 12),
                            bottomRight: Radius.circular(fromDispatch ? 12 : 2),
                          ),
                          boxShadow: [
                            BoxShadow(
                              color: (fromDispatch
                                      ? FmsTheme.cyanAccent
                                      : FmsTheme.emeraldGreen)
                                  .withValues(alpha: 0.2),
                              blurRadius: 8,
                              offset: const Offset(0, 2),
                            ),
                          ],
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Icon(
                                  fromDispatch
                                      ? Icons.headset_mic_rounded
                                      : Icons.local_shipping_rounded,
                                  size: 15,
                                  color: fromDispatch
                                      ? FmsTheme.cyanAccent
                                      : FmsTheme.emeraldGreen,
                                ),
                                const SizedBox(width: 6),
                                Text(
                                  fromDispatch ? 'RUANG KONTROL (DISPATCHER)' : 'KABIN OPERATOR',
                                  style: TextStyle(
                                    color: fromDispatch
                                        ? FmsTheme.cyanAccent
                                        : FmsTheme.emeraldGreen,
                                    fontWeight: FontWeight.w800,
                                    fontSize: 10.5,
                                    letterSpacing: 0.4,
                                  ),
                                ),
                                if (isUrgent) ...[
                                  const SizedBox(width: 8),
                                  Container(
                                    padding: const EdgeInsets.symmetric(
                                      horizontal: 5,
                                      vertical: 1.5,
                                    ),
                                    decoration: BoxDecoration(
                                      color: FmsTheme.redHazard,
                                      borderRadius: BorderRadius.circular(4),
                                    ),
                                    child: const Text(
                                      'DARURAT / URGENT',
                                      style: TextStyle(
                                        color: Colors.white,
                                        fontSize: 8,
                                        fontWeight: FontWeight.w900,
                                      ),
                                    ),
                                  ),
                                ],
                              ],
                            ),
                            const SizedBox(height: 6),
                            voice
                                ? Row(
                                    children: [
                                      const Icon(Icons.volume_up, size: 18, color: FmsTheme.cyanAccent),
                                      const SizedBox(width: 8),
                                      Text(
                                        'Transmisi Suara Langsung',
                                        style: FmsTheme.bodyNormal.copyWith(fontSize: 13),
                                      ),
                                    ],
                                  )
                                : Text(
                                    item['body']?.toString() ?? '',
                                    style: const TextStyle(
                                      fontSize: 14.0,
                                      fontWeight: FontWeight.w600,
                                      color: Colors.white,
                                      height: 1.3,
                                    ),
                                  ),
                            const SizedBox(height: 4),
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
                                  fontSize: 9.0,
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

        // 3. 1-Touch Quick Presets Grid for Operator
        Container(
          height: 38,
          margin: const EdgeInsets.only(bottom: 6),
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: quickPresets.length,
            separatorBuilder: (context, index) => const SizedBox(width: 6),
            itemBuilder: (context, idx) {
              final preset = quickPresets[idx];
              return ElevatedButton(
                onPressed: busy ? null : () => send(preset['text']),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF0A2238),
                  foregroundColor: FmsTheme.cyanAccent,
                  side: BorderSide(color: FmsTheme.cyanAccent.withValues(alpha: 0.6)),
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 0),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                  elevation: 2,
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(preset['icon'], style: const TextStyle(fontSize: 12)),
                    const SizedBox(width: 5),
                    Text(
                      preset['label'],
                      style: const TextStyle(
                        fontSize: 10.5,
                        fontWeight: FontWeight.bold,
                        letterSpacing: 0.3,
                      ),
                    ),
                  ],
                ),
              );
            },
          ),
        ),

        // 4. Custom Message Input
        Row(
          children: [
            Expanded(
              child: SizedBox(
                height: 46,
                child: TextField(
                  controller: messageInput,
                  maxLength: 500,
                  onSubmitted: (_) => send(),
                  style: FmsTheme.bodyNormal.copyWith(fontSize: 13),
                  decoration: InputDecoration(
                    hintText: 'Ketik pesan manual...',
                    hintStyle: FmsTheme.caption.copyWith(fontSize: 11),
                    counterText: '',
                    filled: true,
                    fillColor: const Color(0xFF08192C),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(10),
                      borderSide: const BorderSide(color: FmsTheme.cardBorder),
                    ),
                    contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 6),
            SizedBox(
              width: 46,
              height: 46,
              child: IconButton.filled(
                tooltip: 'Kirim Pesan',
                onPressed: busy ? null : () => send(),
                style: IconButton.styleFrom(
                  backgroundColor: FmsTheme.cyanAccent,
                  foregroundColor: FmsTheme.bgDark,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                ),
                icon: const Icon(Icons.send_rounded, size: 20),
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),

        // 5. Giant PTT (Push-to-Talk) Button with Pulsing Wave & Mic Indicator
        AnimatedBuilder(
          animation: _pulseController,
          builder: (context, child) {
            final isLive = live.speaking || live.requestingMic;
            final isReceiving = live.receivingVoice;

            final Color btnBg = isLive
                ? FmsTheme.redHazard
                : isReceiving
                ? FmsTheme.amberWarning
                : FmsTheme.emeraldGreen;

            final List<BoxShadow>? glow = isLive || isReceiving
                ? [
                    BoxShadow(
                      color: btnBg.withValues(alpha: 0.4 + 0.35 * _pulseController.value),
                      blurRadius: 16 + 8 * _pulseController.value,
                      spreadRadius: 2,
                    ),
                  ]
                : null;

            return Container(
              height: 52,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(12),
                boxShadow: glow,
              ),
              child: FilledButton.icon(
                onPressed: isLive
                    ? live.stopSpeaking
                    : isReceiving
                    ? null
                    : live.startSpeaking,
                style: FilledButton.styleFrom(
                  backgroundColor: btnBg,
                  foregroundColor: FmsTheme.bgDark,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
                icon: Icon(
                  isLive
                      ? Icons.stop_circle_rounded
                      : isReceiving
                      ? Icons.volume_up_rounded
                      : Icons.mic_rounded,
                  size: 24,
                ),
                label: Text(
                  isLive
                      ? '● SEDANG BICARA KE KONTROL (TEKAN UNTUK SELESAI)'
                      : isReceiving
                      ? '🔊 RUANG KONTROL SEDANG BERBICARA...'
                      : '🎙️ TEKAN UNTUK BICARA LANGSUNG (PTT)',
                  style: const TextStyle(
                    fontWeight: FontWeight.w900,
                    fontSize: 11.5,
                    letterSpacing: 0.5,
                  ),
                ),
              ),
            );
          },
        ),
      ],
    );
  }
}

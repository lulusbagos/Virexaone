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
    {'icon': '🚜', 'label': 'SIAP MUAT', 'text': 'Unit telah siap untuk pemuatan material di Front Gali.'},
    {'icon': '🚛', 'label': 'MENUJU FRONT', 'text': 'Unit sedang bergerak menuju ke Front Pemuatan.'},
    {'icon': '🛑', 'label': 'ANTRE DISPOSAL', 'text': 'Unit sedang mengantre untuk pembongkaran muatan di Disposal.'},
    {'icon': '📦', 'label': 'SELESAI DUMP', 'text': 'Pembongkaran muatan selesai. Unit kembali menuju ke Front.'},
    {'icon': '⛽', 'label': 'PERLU BAHAN BAKAR', 'text': 'Bahan bakar unit menipis. Memerlukan pengisian bahan bakar.'},
    {'icon': '⚠️', 'label': 'KENDALA JALUR', 'text': 'Peringatan: Terdapat kendala jalan atau bahaya di jalur pengangkutan.'},
    {'icon': '📢', 'label': 'MOHON INSTRUKSI', 'text': 'Mohon konfirmasi instruksi tugas berikutnya dari Ruang Kontrol.'},
  ];

  Uri endpoint(String path, [Map<String, String>? query]) => Uri.parse(
    '${api.backendBaseUrl.replaceAll(RegExp(r'/+$'), '')}/api/v1/comms/$path',
  ).replace(queryParameters: query);

  @override
  void initState() {
    super.initState();
    _pulseController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 900),
    )..repeat(reverse: true);

    live.addListener(_onLiveChanged);
    _initAutoConnection();

    poll = Timer.periodic(const Duration(seconds: 2), (_) {
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
    final unit = api.selectedUnitId.isEmpty ? 'DT5107' : api.selectedUnitId;
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
        setState(() {
          serviceEnabled = false;
          status = 'Server FMS belum terjangkau';
        });
      }
    } finally {
      isAutoPairing = false;
    }
  }

  Future<void> refresh() async {
    final unit = boundUnit ?? api.selectedUnitId;
    final key = unitKey;
    if (unit.isEmpty || key == null || key.isEmpty) return;

    try {
      final response = await http
          .get(
            endpoint('messages', {'unit_name': unit}),
            headers: {
              'X-FMS-Unit-Key': key,
              'Content-Type': 'application/json',
            },
          )
          .timeout(const Duration(seconds: 4));

      if (response.statusCode == 200 && mounted) {
        final decoded = jsonDecode(response.body);
        if (decoded is Map<String, dynamic> && decoded['data'] is List) {
          final List list = decoded['data'];
          final parsed = list
              .map((item) => Map<String, dynamic>.from(item as Map))
              .toList();

          final prevCount = messages.length;
          setState(() {
            messages = parsed;
            serviceEnabled = true;
            status = 'Radio & Pesan Terhubung';
          });

          if (parsed.length > prevCount) {
            _scrollToBottom();
          }
        }
      }
    } catch (_) {}
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (scrollController.hasClients) {
        scrollController.animateTo(
          scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 300),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> send([String? textOverride]) async {
    final body = (textOverride ?? messageInput.text).trim();
    if (body.isEmpty || busy) return;

    final unit = api.selectedUnitId.isNotEmpty
        ? api.selectedUnitId
        : (boundUnit ?? 'DT5107');
    String? key = unitKey;

    setState(() => busy = true);

    if (key == null || key.isEmpty) {
      try {
        final newKey = await api.ensureCabinCommsPairing(unit);
        if (newKey != null && newKey.isNotEmpty && mounted) {
          key = newKey;
          unitKey = newKey;
          boundUnit = unit;
          serviceEnabled = true;
          status = 'Radio & Pesan Terhubung';
        }
      } catch (_) {}
    }

    if (key == null || key.isEmpty) {
      if (mounted) {
        setState(() {
          busy = false;
          status = 'Kanal kabin belum terhubung ke server FMS';
        });
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('⚠️ Kanal kabin belum terhubung ke server FMS. Memeriksa koneksi...'),
            backgroundColor: FmsTheme.amberWarning,
            duration: Duration(seconds: 2),
          ),
        );
      }
      return;
    }

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
        _scrollToBottom();
      } else {
        throw Exception('Gagal mengirim (HTTP ${response.statusCode})');
      }
    } catch (error) {
      if (mounted) {
        final errText = error.toString().replaceFirst('Exception: ', '');
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('⚠️ $errText', style: const TextStyle(fontSize: 12)),
            backgroundColor: FmsTheme.redHazard,
            duration: const Duration(seconds: 2),
          ),
        );
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final activeUnit = api.selectedUnitId.isEmpty ? 'DT5107' : api.selectedUnitId;
    final isLandscape = MediaQuery.of(context).orientation == Orientation.landscape;

    if (isLandscape) {
      return _buildLandscapeLayout(activeUnit);
    } else {
      return _buildPortraitLayout(activeUnit);
    }
  }

  // =========================================================================
  // 1. LANDSCAPE 2-COLUMN OPTIMIZED IN-CABIN COCKPIT LAYOUT
  // =========================================================================
  Widget _buildLandscapeLayout(String activeUnit) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Left Column: Chat Thread + Quick Presets + Text Input (64% Width)
        Expanded(
          flex: 64,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _buildHeaderStatusBanner(activeUnit),
              const SizedBox(height: 6),
              Expanded(child: _buildMessagesList()),
              const SizedBox(height: 6),
              _buildQuickPresetsBar(),
              const SizedBox(height: 6),
              _buildTextInputBar(),
            ],
          ),
        ),
        const SizedBox(width: 8),

        // Right Column: Dedicated Radio PTT & Audio Transceiver (36% Width)
        Expanded(
          flex: 36,
          child: _buildRightPttStation(activeUnit),
        ),
      ],
    );
  }

  // =========================================================================
  // 2. PORTRAIT COLLAPSIBLE ADAPTIVE LAYOUT
  // =========================================================================
  Widget _buildPortraitLayout(String activeUnit) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _buildHeaderStatusBanner(activeUnit),
        const SizedBox(height: 6),
        Expanded(child: _buildMessagesList()),
        const SizedBox(height: 6),
        _buildQuickPresetsBar(),
        const SizedBox(height: 6),
        _buildTextInputBar(),
        const SizedBox(height: 6),
        _buildCompactPttButton(),
      ],
    );
  }

  // =========================================================================
  // UI COMPONENTS: HEADER, CHAT, PRESETS, INPUT & PTT
  // =========================================================================

  Widget _buildHeaderStatusBanner(String activeUnit) {
    final isOnline = live.connected || serviceEnabled;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: FmsTheme.cardHeaderBg,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(
          color: isOnline
              ? FmsTheme.emeraldGreen.withValues(alpha: 0.6)
              : FmsTheme.amberWarning.withValues(alpha: 0.6),
          width: 1.0,
        ),
      ),
      child: Row(
        children: [
          Container(
            width: 8,
            height: 8,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: isOnline ? FmsTheme.emeraldGreen : FmsTheme.amberWarning,
              boxShadow: FmsTheme.neonGlowShadow(
                isOnline ? FmsTheme.emeraldGreen : FmsTheme.amberWarning,
                opacity: 0.8,
                blur: 5,
              ),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              'KANAL KABIN: $activeUnit  •  ${isOnline ? "TERHUBUNG DUA ARAH" : status}',
              style: FmsTheme.caption.copyWith(
                color: isOnline ? FmsTheme.emeraldGreen : FmsTheme.amberWarning,
                fontWeight: FontWeight.bold,
                fontSize: 10.5,
              ),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
          InkWell(
            onTap: () {
              _initAutoConnection();
              refresh();
            },
            borderRadius: BorderRadius.circular(4),
            child: Padding(
              padding: const EdgeInsets.all(3),
              child: Icon(Icons.refresh, size: 16, color: FmsTheme.cyanAccent),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildMessagesList() {
    if (messages.isEmpty) {
      return Container(
        decoration: BoxDecoration(
          color: const Color(0xFF06141D),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: FmsTheme.cardBorder),
        ),
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                Icons.chat_bubble_outline_rounded,
                size: 32,
                color: FmsTheme.cyanAccent.withValues(alpha: 0.35),
              ),
              const SizedBox(height: 6),
              Text(
                'Belum ada pesan masuk.',
                style: FmsTheme.titleMedium.copyWith(color: FmsTheme.textLight, fontSize: 12),
              ),
              const SizedBox(height: 2),
              Text(
                'Pesan dari Kontrol atau Kabin akan tampil otomatis di sini.',
                style: FmsTheme.caption.copyWith(fontSize: 10),
              ),
            ],
          ),
        ),
      );
    }

    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFF06141D),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: FmsTheme.cardBorder),
      ),
      child: ListView.builder(
        controller: scrollController,
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
        itemCount: messages.length,
        itemBuilder: (context, index) {
          final item = messages[index];
          final fromDispatch = item['sender_role'] == 'dispatcher';
          final isUrgent = item['priority'] == 'urgent';
          final msgId = item['id']?.toString() ?? 'msg_$index';
          final body = item['body']?.toString() ?? '';
          final isPlayingThis = (live.isTtsSpeaking && live.activePlayingMessageId == msgId) ||
              (live.receivingVoice && fromDispatch && index == messages.length - 1);

          final align = fromDispatch ? Alignment.centerLeft : Alignment.centerRight;
          final Color bubbleBg = fromDispatch
              ? (isUrgent ? const Color(0x33FF2A55) : const Color(0xFF0D253A))
              : const Color(0xFF073024);

          final Color borderClr = fromDispatch
              ? (isUrgent ? FmsTheme.redHazard : FmsTheme.cyanAccent)
              : FmsTheme.emeraldGreen;

          return Align(
            alignment: align,
            child: Container(
              margin: const EdgeInsets.only(bottom: 7),
              constraints: const BoxConstraints(maxWidth: 420),
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              decoration: BoxDecoration(
                color: bubbleBg,
                borderRadius: BorderRadius.circular(8),
                border: Border.all(
                  color: isPlayingThis
                      ? FmsTheme.emeraldGreen
                      : borderClr.withValues(alpha: 0.7),
                  width: isPlayingThis ? 1.5 : 1.0,
                ),
                boxShadow: isPlayingThis
                    ? [
                        BoxShadow(
                          color: FmsTheme.emeraldGreen.withValues(alpha: 0.35),
                          blurRadius: 8,
                          spreadRadius: 1,
                        ),
                      ]
                    : null,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Icon(
                        fromDispatch ? Icons.headset_mic_rounded : Icons.local_shipping_rounded,
                        size: 13,
                        color: borderClr,
                      ),
                      const SizedBox(width: 5),
                      Text(
                        fromDispatch ? 'RUANG KONTROL (DISPATCH)' : 'OPERATOR KABIN',
                        style: TextStyle(
                          color: borderClr,
                          fontSize: 9.0,
                          fontWeight: FontWeight.w800,
                          letterSpacing: 0.5,
                        ),
                      ),
                      if (fromDispatch) ...[
                        const Spacer(),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1.5),
                          decoration: BoxDecoration(
                            color: isPlayingThis ? const Color(0x3300FFA3) : const Color(0x2200E5FF),
                            borderRadius: BorderRadius.circular(4),
                            border: Border.all(
                              color: isPlayingThis
                                  ? FmsTheme.emeraldGreen
                                  : FmsTheme.cyanAccent.withValues(alpha: 0.5),
                              width: 0.8,
                            ),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Icon(
                                isPlayingThis ? Icons.volume_up_rounded : Icons.graphic_eq_rounded,
                                size: 10,
                                color: isPlayingThis ? FmsTheme.emeraldGreen : FmsTheme.cyanAccent,
                              ),
                              const SizedBox(width: 3),
                              Text(
                                isPlayingThis ? 'MEMUTAR SUARA' : 'AUDIO AKTIF',
                                style: TextStyle(
                                  color: isPlayingThis ? FmsTheme.emeraldGreen : FmsTheme.cyanAccent,
                                  fontSize: 7.5,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    body,
                    style: const TextStyle(
                      color: Color(0xFFF0F6FC),
                      fontSize: 11.5,
                      fontWeight: FontWeight.w500,
                      height: 1.25,
                    ),
                  ),
                  if (fromDispatch) ...[
                    const SizedBox(height: 6),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
                      decoration: BoxDecoration(
                        color: const Color(0xE0051422),
                        borderRadius: BorderRadius.circular(6),
                        border: Border.all(
                          color: isPlayingThis
                              ? FmsTheme.emeraldGreen.withValues(alpha: 0.8)
                              : const Color(0xFF13384D),
                          width: 1.0,
                        ),
                      ),
                      child: Row(
                        children: [
                          // Replay / Play Button
                          GestureDetector(
                            behavior: HitTestBehavior.opaque,
                            onTap: () => live.replayMessage(body, messageId: msgId),
                            child: Container(
                              padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
                              decoration: BoxDecoration(
                                color: isPlayingThis
                                    ? FmsTheme.emeraldGreen.withValues(alpha: 0.25)
                                    : const Color(0xFF0E3048),
                                borderRadius: BorderRadius.circular(6),
                                border: Border.all(
                                  color: isPlayingThis ? FmsTheme.emeraldGreen : FmsTheme.cyanAccent,
                                  width: 1.1,
                                ),
                              ),
                              child: Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Icon(
                                    isPlayingThis ? Icons.stop_rounded : Icons.play_arrow_rounded,
                                    size: 15,
                                    color: isPlayingThis ? FmsTheme.emeraldGreen : FmsTheme.cyanAccent,
                                  ),
                                  const SizedBox(width: 4),
                                  Text(
                                    isPlayingThis ? 'STOP' : 'PUTAR ULANG',
                                    style: TextStyle(
                                      color: isPlayingThis ? FmsTheme.emeraldGreen : FmsTheme.cyanAccent,
                                      fontSize: 9.0,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),

                          // Dynamic Waveform / Spectrum Visualizer
                          AudioSpectrumVisualizer(
                            isPlaying: isPlayingThis,
                            activeColor: isUrgent ? const Color(0xFFFF3366) : const Color(0xFF00FFA3),
                            barCount: 16,
                            height: 16,
                            barWidth: 2.2,
                          ),
                          const Spacer(),

                          Text(
                            isPlayingThis ? '🔊 SUARA TERDENGAR' : 'TERKIRIM',
                            style: TextStyle(
                              color: isPlayingThis ? FmsTheme.emeraldGreen : FmsTheme.textMuted,
                              fontSize: 7.5,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                  const SizedBox(height: 3),
                  Align(
                    alignment: Alignment.bottomRight,
                    child: Text(
                      item['sent_at']?.toString().replaceFirst('T', ' ').split('.').first ?? '',
                      style: FmsTheme.caption.copyWith(fontSize: 8.0, color: FmsTheme.textMuted),
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }

  Widget _buildQuickPresetsBar() {
    return SizedBox(
      height: 34,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: quickPresets.length,
        separatorBuilder: (context, index) => const SizedBox(width: 6),
        itemBuilder: (context, idx) {
          final preset = quickPresets[idx];
          return Material(
            color: Colors.transparent,
            child: InkWell(
              onTap: busy ? null : () => send(preset['text']),
              borderRadius: BorderRadius.circular(6),
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                decoration: BoxDecoration(
                  color: const Color(0xFF0A2238),
                  borderRadius: BorderRadius.circular(6),
                  border: Border.all(color: FmsTheme.cyanAccent.withValues(alpha: 0.6)),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(preset['icon'], style: const TextStyle(fontSize: 12)),
                    const SizedBox(width: 5),
                    Text(
                      preset['label'],
                      style: TextStyle(
                        fontSize: 10,
                        fontWeight: FontWeight.w700,
                        color: FmsTheme.cyanAccent,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }

  Widget _buildTextInputBar() {
    return Row(
      children: [
        Expanded(
          child: SizedBox(
            height: 42,
            child: TextField(
              controller: messageInput,
              maxLength: 500,
              enableSuggestions: true,
              autocorrect: false,
              textInputAction: TextInputAction.send,
              onSubmitted: (_) => send(),
              style: FmsTheme.bodyNormal.copyWith(fontSize: 12),
              decoration: InputDecoration(
                hintText: 'Ketik pesan ke ruang kontrol...',
                hintStyle: FmsTheme.caption.copyWith(fontSize: 11),
                counterText: '',
                filled: true,
                fillColor: const Color(0xFF08192C),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: BorderSide(color: FmsTheme.cardBorder),
                ),
                enabledBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: BorderSide(color: FmsTheme.cardBorder),
                ),
                focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: BorderSide(color: FmsTheme.cyanAccent, width: 1.5),
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              ),
            ),
          ),
        ),
        const SizedBox(width: 6),
        SizedBox(
          width: 42,
          height: 42,
          child: ElevatedButton(
            onPressed: busy ? null : () => send(),
            style: ElevatedButton.styleFrom(
              backgroundColor: FmsTheme.cyanAccent,
              foregroundColor: FmsTheme.bgDark,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
              padding: EdgeInsets.zero,
            ),
            child: busy
                ? SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2, color: FmsTheme.bgDark),
                  )
                : const Icon(Icons.send_rounded, size: 19),
          ),
        ),
      ],
    );
  }

  Widget _buildRightPttStation(String activeUnit) {
    return AnimatedBuilder(
      animation: Listenable.merge([live, _pulseController]),
      builder: (context, child) {
        final isLive = live.speaking || live.requestingMic;
        final isReceiving = live.receivingVoice;
        final isTtsActive = live.isTtsSpeaking;

        final Color themeColor = isLive
            ? FmsTheme.redHazard
            : (isReceiving || isTtsActive)
            ? FmsTheme.amberWarning
            : FmsTheme.emeraldGreen;

        return Container(
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            color: FmsTheme.cardBg,
            borderRadius: BorderRadius.circular(10),
            border: Border.all(
              color: isLive || isReceiving || isTtsActive ? themeColor : FmsTheme.cardBorder,
              width: isLive || isReceiving || isTtsActive ? 1.5 : 1.0,
            ),
            boxShadow: isLive || isReceiving || isTtsActive
                ? [
                    BoxShadow(
                      color: themeColor.withValues(alpha: 0.35 * _pulseController.value),
                      blurRadius: 16,
                      spreadRadius: 2,
                    ),
                  ]
                : null,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Icon(Icons.radio_rounded, size: 16, color: themeColor),
                  const SizedBox(width: 6),
                  Text(
                    'RADIO DISPATCH',
                    style: TextStyle(
                      color: themeColor,
                      fontSize: 10.5,
                      fontWeight: FontWeight.w900,
                      letterSpacing: 0.4,
                    ),
                  ),
                ],
              ),
              Text(
                'Kanal: UTAMA • 16.0 kHz PCM16',
                style: FmsTheme.caption.copyWith(fontSize: 8.5, color: FmsTheme.textMuted),
              ),
              const Spacer(),

              // Giant PTT Button Center
              Center(
                child: GestureDetector(
                  behavior: HitTestBehavior.opaque,
                  onTapDown: (_) {
                    if (!live.speaking && !live.receivingVoice) live.startSpeaking();
                  },
                  onTapUp: (_) {
                    if (live.speaking || live.requestingMic) live.stopSpeaking();
                  },
                  onTapCancel: () {
                    if (live.speaking || live.requestingMic) live.stopSpeaking();
                  },
                  child: Container(
                    width: 90,
                    height: 90,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: themeColor.withValues(alpha: isLive ? 0.9 : 0.2),
                      border: Border.all(color: themeColor, width: 3),
                      boxShadow: [
                        BoxShadow(
                          color: themeColor.withValues(
                            alpha: isLive
                                ? 0.6 + 0.3 * _pulseController.value
                                : 0.25,
                          ),
                          blurRadius: isLive ? 22 : 10,
                          spreadRadius: isLive ? 4 : 1,
                        ),
                      ],
                    ),
                    child: Center(
                      child: Icon(
                        isLive
                            ? Icons.stop_circle_rounded
                            : isReceiving || isTtsActive
                            ? Icons.volume_up_rounded
                            : Icons.mic_rounded,
                        size: 42,
                        color: isLive ? Colors.white : themeColor,
                      ),
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 8),

              Text(
                isLive
                    ? '● TRANSMISI SUARA AKTIF\n(LEPAS UNTUK SELESAI)'
                    : isReceiving || isTtsActive
                    ? '🔊 RUANG KONTROL SEDANG BERBICARA...'
                    : 'TEKAN & TAHAN MIC\nUNTUK BICARA KE KONTROL',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: isLive || isReceiving || isTtsActive ? themeColor : FmsTheme.textLight,
                  fontSize: 9.5,
                  fontWeight: FontWeight.w800,
                  height: 1.2,
                ),
              ),
              const SizedBox(height: 8),

              // Live Equalizer Spectrum in Radio Station
              Center(
                child: AudioSpectrumVisualizer(
                  isPlaying: isLive || isReceiving || isTtsActive,
                  activeColor: isLive
                      ? const Color(0xFFFF3366)
                      : isReceiving || isTtsActive
                      ? const Color(0xFFFFB703)
                      : const Color(0xFF00FFA3),
                  barCount: 22,
                  height: 20,
                  barWidth: 3.0,
                ),
              ),
              const Spacer(),

              Container(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
                decoration: BoxDecoration(
                  color: const Color(0xFF05111B),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Text(
                  live.status,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: live.connected ? FmsTheme.emeraldGreen : FmsTheme.amberWarning,
                    fontSize: 8.5,
                    fontWeight: FontWeight.w600,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _buildCompactPttButton() {
    return AnimatedBuilder(
      animation: Listenable.merge([live, _pulseController]),
      builder: (context, child) {
        final isLive = live.speaking || live.requestingMic;
        final isReceiving = live.receivingVoice;
        final isTtsActive = live.isTtsSpeaking;

        final Color btnBg = isLive
            ? FmsTheme.redHazard
            : isReceiving || isTtsActive
            ? FmsTheme.amberWarning
            : FmsTheme.emeraldGreen;

        return SizedBox(
          height: 44,
          child: FilledButton.icon(
            onPressed: isLive
                ? live.stopSpeaking
                : isReceiving
                ? null
                : live.startSpeaking,
            style: FilledButton.styleFrom(
              backgroundColor: btnBg,
              foregroundColor: FmsTheme.bgDark,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
            icon: Icon(
              isLive ? Icons.stop_rounded : Icons.mic_rounded,
              size: 20,
            ),
            label: Text(
              isLive
                  ? '● SEDANG TRANSMISI SUARA (TEKAN SELESAI)'
                  : isReceiving || isTtsActive
                  ? '🔊 KONTROL SEDANG BICARA...'
                  : '🎙️ TEKAN UNTUK BICARA PTT',
              style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 10.5),
            ),
          ),
        );
      },
    );
  }
}

// =========================================================================
// AUDIO SPECTRUM WAVEFORM VISUALIZER COMPONENT
// =========================================================================
class AudioSpectrumVisualizer extends StatefulWidget {
  final bool isPlaying;
  final Color activeColor;
  final Color inactiveColor;
  final int barCount;
  final double height;
  final double barWidth;

  const AudioSpectrumVisualizer({
    super.key,
    required this.isPlaying,
    this.activeColor = const Color(0xFF00FFA3),
    this.inactiveColor = const Color(0xFF163E50),
    this.barCount = 12,
    this.height = 18,
    this.barWidth = 2.4,
  });

  @override
  State<AudioSpectrumVisualizer> createState() => _AudioSpectrumVisualizerState();
}

class _AudioSpectrumVisualizerState extends State<AudioSpectrumVisualizer>
    with SingleTickerProviderStateMixin {
  late AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 650),
    );
    if (widget.isPlaying) {
      _controller.repeat();
    }
  }

  @override
  void didUpdateWidget(AudioSpectrumVisualizer oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.isPlaying != oldWidget.isPlaying) {
      if (widget.isPlaying) {
        _controller.repeat();
      } else {
        _controller.stop();
        _controller.reset();
      }
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, _) {
        final phase = _controller.value * 2 * 3.141592653589793;
        return SizedBox(
          height: widget.height,
          child: Row(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: List.generate(widget.barCount, (i) {
              double ratio;
              if (widget.isPlaying) {
                final wave1 = 0.5 + 0.5 * (((i * 0.9 + phase * 2).abs() % 3.14159 - 1.57079).abs());
                final wave2 = 0.3 + 0.7 * (((i * 1.4 - phase * 3) % 6.28).abs() / 6.28);
                ratio = ((wave1 + wave2) / 2).clamp(0.20, 1.0);
              } else {
                final centerDist = (i - widget.barCount / 2).abs() / (widget.barCount / 2);
                ratio = (0.45 - 0.25 * centerDist).clamp(0.2, 0.5);
              }

              final barH = widget.height * ratio;

              return Container(
                margin: const EdgeInsets.symmetric(horizontal: 1.1),
                width: widget.barWidth,
                height: barH,
                decoration: BoxDecoration(
                  color: widget.isPlaying ? widget.activeColor : widget.inactiveColor,
                  borderRadius: BorderRadius.circular(2),
                  boxShadow: widget.isPlaying
                      ? [
                          BoxShadow(
                            color: widget.activeColor.withValues(alpha: 0.6),
                            blurRadius: 4,
                            spreadRadius: 0.5,
                          ),
                        ]
                      : null,
                ),
              );
            }),
          ),
        );
      },
    );
  }
}

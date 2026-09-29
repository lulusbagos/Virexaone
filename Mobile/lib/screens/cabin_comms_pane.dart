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
      duration: const Duration(milliseconds: 900),
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
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> refresh() async {
    final key = unitKey ?? await api.ensureCabinCommsPairing(boundUnit);
    final unit = boundUnit ?? (api.selectedUnitId.isEmpty ? 'RD5100' : api.selectedUnitId);
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
    String? key = unitKey;
    final unit = boundUnit ?? (api.selectedUnitId.isEmpty ? 'RD5100' : api.selectedUnitId);
    final body = (textOverride ?? messageInput.text).trim();
    if (body.isEmpty || busy) return;

    setState(() => busy = true);
    try {
      if (key == null || key.isEmpty) {
        key = await api.ensureCabinCommsPairing(unit);
        if (key != null) unitKey = key;
      }
      if (key == null || key.isEmpty) {
        throw Exception('Belum terhubung ke server FMS');
      }

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
    final activeUnit = api.selectedUnitId.isEmpty ? 'RD5100' : api.selectedUnitId;
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
              // Header Status Banner
              _buildHeaderStatusBanner(activeUnit),
              const SizedBox(height: 6),

              // Chat Message List
              Expanded(child: _buildMessagesList()),
              const SizedBox(height: 6),

              // Quick Preset Buttons Bar
              _buildQuickPresetsBar(),
              const SizedBox(height: 6),

              // Text Field Input Bar
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
              'KANAL KABIN: $activeUnit  •  ${isOnline ? "TERHUBUNG LIVE" : status}',
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
              child: const Icon(Icons.refresh, size: 16, color: FmsTheme.cyanAccent),
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
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
        itemCount: messages.length,
        itemBuilder: (context, index) {
          final item = messages[index];
          final fromDispatch = item['sender_role'] == 'dispatcher';
          final voice = item['kind'] == 'voice';
          final priority = item['priority']?.toString();
          final isUrgent = priority == 'urgent';

          return Align(
            alignment: fromDispatch ? Alignment.centerLeft : Alignment.centerRight,
            child: Container(
              constraints: const BoxConstraints(maxWidth: 420),
              margin: const EdgeInsets.only(bottom: 6),
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: fromDispatch
                    ? const Color(0xF2082236)
                    : const Color(0xF2062C20),
                border: Border.all(
                  color: isUrgent
                      ? FmsTheme.redHazard
                      : fromDispatch
                          ? FmsTheme.cyanAccent.withValues(alpha: 0.7)
                          : FmsTheme.emeraldGreen.withValues(alpha: 0.7),
                  width: isUrgent ? 1.8 : 1.0,
                ),
                borderRadius: BorderRadius.only(
                  topLeft: const Radius.circular(8),
                  topRight: const Radius.circular(8),
                  bottomLeft: Radius.circular(fromDispatch ? 2 : 8),
                  bottomRight: Radius.circular(fromDispatch ? 8 : 2),
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(
                        fromDispatch ? Icons.headset_mic_rounded : Icons.local_shipping_rounded,
                        size: 13,
                        color: fromDispatch ? FmsTheme.cyanAccent : FmsTheme.emeraldGreen,
                      ),
                      const SizedBox(width: 5),
                      Text(
                        fromDispatch ? 'RUANG KONTROL' : 'KABIN SAYA',
                        style: TextStyle(
                          color: fromDispatch ? FmsTheme.cyanAccent : FmsTheme.emeraldGreen,
                          fontWeight: FontWeight.w800,
                          fontSize: 9.5,
                        ),
                      ),
                      if (isUrgent) ...[
                        const SizedBox(width: 6),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
                          decoration: BoxDecoration(
                            color: FmsTheme.redHazard,
                            borderRadius: BorderRadius.circular(3),
                          ),
                          child: const Text(
                            'URGENT',
                            style: TextStyle(color: Colors.white, fontSize: 7.5, fontWeight: FontWeight.bold),
                          ),
                        ),
                      ],
                    ],
                  ),
                  const SizedBox(height: 3),
                  voice
                      ? Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(Icons.volume_up, size: 15, color: FmsTheme.cyanAccent),
                            const SizedBox(width: 6),
                            Text(
                              'Transmisi Suara PTT',
                              style: FmsTheme.bodyNormal.copyWith(fontSize: 11.5),
                            ),
                          ],
                        )
                      : Text(
                          item['body']?.toString() ?? '',
                          style: const TextStyle(
                            fontSize: 12.0,
                            fontWeight: FontWeight.w600,
                            color: Colors.white,
                            height: 1.25,
                          ),
                        ),
                  const SizedBox(height: 2),
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
      height: 32,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: quickPresets.length,
        separatorBuilder: (context, index) => const SizedBox(width: 5),
        itemBuilder: (context, idx) {
          final preset = quickPresets[idx];
          return InkWell(
            onTap: busy ? null : () => send(preset['text']),
            borderRadius: BorderRadius.circular(6),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: const Color(0xFF0A2238),
                borderRadius: BorderRadius.circular(6),
                border: Border.all(color: FmsTheme.cyanAccent.withValues(alpha: 0.5)),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(preset['icon'], style: const TextStyle(fontSize: 11)),
                  const SizedBox(width: 4),
                  Text(
                    preset['label'],
                    style: const TextStyle(
                      fontSize: 9.5,
                      fontWeight: FontWeight.w700,
                      color: FmsTheme.cyanAccent,
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

  Widget _buildTextInputBar() {
    return Row(
      children: [
        Expanded(
          child: SizedBox(
            height: 38,
            child: TextField(
              controller: messageInput,
              maxLength: 500,
              onSubmitted: (_) => send(),
              style: FmsTheme.bodyNormal.copyWith(fontSize: 12),
              decoration: InputDecoration(
                hintText: 'Ketik pesan manual ke kontrol...',
                hintStyle: FmsTheme.caption.copyWith(fontSize: 10.5),
                counterText: '',
                filled: true,
                fillColor: const Color(0xFF08192C),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: const BorderSide(color: FmsTheme.cardBorder),
                ),
                enabledBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: const BorderSide(color: FmsTheme.cardBorder),
                ),
                focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: const BorderSide(color: FmsTheme.cyanAccent),
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              ),
            ),
          ),
        ),
        const SizedBox(width: 5),
        SizedBox(
          width: 38,
          height: 38,
          child: IconButton.filled(
            
            onPressed: busy ? null : () => send(),
            style: IconButton.styleFrom(
              backgroundColor: FmsTheme.cyanAccent,
              foregroundColor: FmsTheme.bgDark,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
              padding: EdgeInsets.zero,
            ),
            icon: busy
                ? const SizedBox(
                    width: 14,
                    height: 14,
                    child: CircularProgressIndicator(strokeWidth: 2, color: FmsTheme.bgDark),
                  )
                : const Icon(Icons.send_rounded, size: 17),
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

        final Color themeColor = isLive
            ? FmsTheme.redHazard
            : isReceiving
            ? FmsTheme.amberWarning
            : FmsTheme.emeraldGreen;

        return Container(
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            color: FmsTheme.cardBg,
            borderRadius: BorderRadius.circular(10),
            border: Border.all(
              color: isLive || isReceiving
                  ? themeColor
                  : FmsTheme.cardBorder,
              width: isLive || isReceiving ? 1.5 : 1.0,
            ),
            boxShadow: isLive || isReceiving
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
              // Header Radio Station
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
                            : isReceiving
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

              // PTT Label Description
              Text(
                isLive
                    ? '● TRANSMISI SUARA AKTIF\n(LEPAS UNTUK SELESAI)'
                    : isReceiving
                    ? '🔊 RUANG KONTROL SEDANG BERBICARA...'
                    : 'TEKAN & TAHAN MIC\nUNTUK BICARA KE KONTROL',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: isLive || isReceiving ? themeColor : FmsTheme.textLight,
                  fontSize: 9.5,
                  fontWeight: FontWeight.w800,
                  height: 1.2,
                ),
              ),
              const Spacer(),

              // Bottom status bar
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

        final Color btnBg = isLive
            ? FmsTheme.redHazard
            : isReceiving
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
                  : isReceiving
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

import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_pcm_sound/flutter_pcm_sound.dart';
import 'package:flutter_tts/flutter_tts.dart';
import 'package:http/http.dart' as http;
import 'package:record/record.dart';
import 'package:web_socket_channel/io.dart';

class CabinAlert {
  final String title;
  final String body;
  final bool voice;
  final bool urgent;
  const CabinAlert(
    this.title,
    this.body, {
    this.voice = false,
    this.urgent = false,
  });
}

class LiveCabinCommsService extends ChangeNotifier {
  static final LiveCabinCommsService _instance = LiveCabinCommsService._();
  factory LiveCabinCommsService() => _instance;
  LiveCabinCommsService._();

  final AudioRecorder _recorder = AudioRecorder();
  final FlutterTts _tts = FlutterTts();
  IOWebSocketChannel? _channel;
  StreamSubscription<dynamic>? _socketSubscription;
  StreamSubscription<Uint8List>? _micSubscription;
  Future<void>? _speakerReady;
  Timer? _retry;
  Timer? _dismiss;
  Timer? _messagePoll;
  final Set<String> _seenMessageIds = {};
  String? _pendingAnnouncement;
  String _baseUrl = '';
  String _unit = '';
  String _key = '';
  int _generation = 0;
  bool connected = false;
  bool speaking = false;
  bool requestingMic = false;
  bool receivingVoice = false;
  String status = 'Belum terhubung';
  CabinAlert? alert;
  String get unit => _unit;

  Future<void> connect(String baseUrl, String unit, String key) async {
    if (_baseUrl == baseUrl && _unit == unit && _key == key) return;
    await disconnect();
    _baseUrl = baseUrl;
    _unit = unit;
    _key = key;
    await _pollMessages(initial: true);
    _messagePoll = Timer.periodic(
      const Duration(seconds: 5),
      (_) => _pollMessages(),
    );
    _open(++_generation);
  }

  Future<void> _open(int generation) async {
    if (generation != _generation || _unit.isEmpty) return;
    try {
      final response = await http
          .post(
            Uri.parse('$_baseUrl/api/v1/comms/live-ticket'),
            headers: {
              'X-FMS-Unit-Key': _key,
              'Content-Type': 'application/json',
            },
            body: jsonEncode({'unit_name': _unit}),
          )
          .timeout(const Duration(seconds: 10));
      if (response.statusCode != 200) {
        throw Exception('HTTP ${response.statusCode}');
      }
      final ticket = (jsonDecode(response.body) as Map)['ticket']?.toString();
      if (ticket == null || generation != _generation) return;
      final wsBase = _baseUrl.replaceFirst(RegExp(r'^https:'), 'wss:').replaceFirst(RegExp(r'^http:'), 'ws:');
      final channel = IOWebSocketChannel.connect(
        '$wsBase/api/v1/comms/live?ticket=${Uri.encodeQueryComponent(ticket)}',
        pingInterval: const Duration(seconds: 20),
        connectTimeout: const Duration(seconds: 10),
      );
      await channel.ready;
      if (generation != _generation) {
        await channel.sink.close();
        return;
      }
      _channel = channel;
      connected = true;
      status = 'Radio langsung terhubung';
      notifyListeners();
      _socketSubscription = channel.stream.listen(
        _onFrame,
        onError: (_) => _onClosed(generation),
        onDone: () => _onClosed(generation),
      );
    } catch (_) {
      if (generation != _generation) return;
      connected = false;
      status = 'Radio terputus. Menghubungkan ulang...';
      notifyListeners();
      _scheduleRetry(generation);
    }
  }

  void _onFrame(dynamic frame) {
    if (frame is List<int>) {
      if (receivingVoice) {
        final bytes = Uint8List.fromList(frame);
        if (bytes.lengthInBytes.isEven) {
          _speakerReady
              ?.then(
                (_) => FlutterPcmSound.feed(
                  PcmArrayInt16(bytes: bytes.buffer.asByteData()),
                ),
              )
              .catchError((_) {});
        }
      }
      return;
    }
    if (frame is! String) return;
    Map<String, dynamic> data;
    try {
      data = jsonDecode(frame) as Map<String, dynamic>;
    } catch (_) {
      return;
    }
    switch (data['type']) {
      case 'message':
        final message = data['data'];
        if (message is Map) _announceMessage(message);
      case 'voice_start':
        if (data['sender_role'] == 'dispatcher') {
          receivingVoice = true;
          alert = const CabinAlert(
            'RADIO RUANG KONTROL',
            'Suara langsung masuk ke kabin',
            voice: true,
          );
          _dismiss?.cancel();
          _tts.stop();
          _speakerReady = FlutterPcmSound.setup(
            sampleRate: 16000,
            channelCount: 1,
          ).catchError((_) {});
          notifyListeners();
        }
      case 'voice_stop':
        receivingVoice = false;
        _dismissLater();
        notifyListeners();
        final pending = _pendingAnnouncement;
        _pendingAnnouncement = null;
        if (pending != null) _speakText(pending);
      case 'busy':
        stopSpeaking();
        status = 'Kanal sedang dipakai';
        notifyListeners();
      case 'ptt_ready':
        if (requestingMic) _beginMicrophone();
    }
  }

  Future<void> _speakText(String body) async {
    if (body.isEmpty) return;
    if (receivingVoice) {
      _pendingAnnouncement = body;
      return;
    }
    try {
      await _tts.setLanguage('id-ID');
      await _tts.setSpeechRate(0.48);
      await _tts.setVolume(1);
      await _tts.speak(body);
    } catch (_) {}
  }

  void _announceMessage(Map message) {
    final id = message['id']?.toString() ?? '';
    if (id.isNotEmpty && !_seenMessageIds.add(id)) return;
    if (message['sender_role'] != 'dispatcher' || message['kind'] != 'text') {
      return;
    }
    final body = message['body']?.toString() ?? '';
    if (body.isEmpty) return;
    alert = CabinAlert(
      'PESAN RUANG KONTROL',
      body,
      urgent: message['priority'] == 'urgent',
    );
    _dismissLater();
    notifyListeners();
    _speakText(body);
  }

  Future<void> _pollMessages({bool initial = false}) async {
    final unit = _unit;
    final key = _key;
    if (unit.isEmpty || key.isEmpty) return;
    try {
      final response = await http
          .get(
            Uri.parse('$_baseUrl/api/v1/comms/messages')
                .replace(queryParameters: {'unit_name': unit}),
            headers: {'X-FMS-Unit-Key': key},
          )
          .timeout(const Duration(seconds: 8));
      if (response.statusCode != 200 || unit != _unit || key != _key) return;
      final items = (jsonDecode(response.body) as Map)['data'] as List;
      for (final item in items) {
        if (item is! Map) continue;
        if (initial) {
          _seenMessageIds.add(item['id']?.toString() ?? '');
        } else {
          _announceMessage(item);
        }
      }
      if (_seenMessageIds.length > 300) {
        _seenMessageIds.removeAll(
          _seenMessageIds.take(_seenMessageIds.length - 200),
        );
      }
    } catch (_) {}
  }

  void _dismissLater() {
    _dismiss?.cancel();
    _dismiss = Timer(const Duration(seconds: 12), dismissAlert);
  }

  void dismissAlert() {
    _dismiss?.cancel();
    alert = null;
    notifyListeners();
  }

  Future<void> startSpeaking() async {
    if (!connected ||
        speaking ||
        requestingMic ||
        receivingVoice ||
        _channel == null) {
      return;
    }
    if (!await _recorder.hasPermission()) {
      status = 'Izin mikrofon ditolak';
      notifyListeners();
      return;
    }
    requestingMic = true;
    status = 'Meminta kanal radio...';
    notifyListeners();
    _channel!.sink.add('{"type":"ptt_start"}');
  }

  Future<void> _beginMicrophone() async {
    if (!requestingMic || !connected) return;
    try {
      final stream = await _recorder.startStream(
        const RecordConfig(
          encoder: AudioEncoder.pcm16bits,
          sampleRate: 16000,
          numChannels: 1,
          echoCancel: true,
          noiseSuppress: true,
        ),
      );
      if (!requestingMic || !connected) {
        await _recorder.stop();
        return;
      }
      requestingMic = false;
      speaking = true;
      status = 'Berbicara ke ruang kontrol';
      notifyListeners();
      _micSubscription = stream.listen((bytes) {
        if (speaking && connected && bytes.isNotEmpty && bytes.length <= 8192) {
          _channel?.sink.add(bytes);
        }
      });
    } catch (_) {
      requestingMic = false;
      _channel?.sink.add('{"type":"ptt_stop"}');
      status = 'Mikrofon tidak dapat dibuka';
      notifyListeners();
    }
  }

  Future<void> stopSpeaking() async {
    if (!speaking && !requestingMic) return;
    requestingMic = false;
    speaking = false;
    await _micSubscription?.cancel();
    _micSubscription = null;
    try {
      await _recorder.stop();
    } catch (_) {}
    _channel?.sink.add('{"type":"ptt_stop"}');
    status = connected ? 'Radio langsung terhubung' : 'Radio terputus';
    notifyListeners();
  }

  void _onClosed(int generation) {
    if (generation != _generation) return;
    connected = false;
    receivingVoice = false;
    requestingMic = false;
    stopSpeaking();
    status = 'Radio terputus. Menghubungkan ulang...';
    notifyListeners();
    _scheduleRetry(generation);
  }

  void _scheduleRetry(int generation) {
    _retry?.cancel();
    _retry = Timer(const Duration(seconds: 4), () => _open(generation));
  }

  Future<void> disconnect() async {
    _generation++;
    _retry?.cancel();
    _dismiss?.cancel();
    _messagePoll?.cancel();
    _seenMessageIds.clear();
    _pendingAnnouncement = null;
    await stopSpeaking();
    await _socketSubscription?.cancel();
    await _channel?.sink.close();
    _socketSubscription = null;
    _channel = null;
    _unit = '';
    _key = '';
    connected = false;
    receivingVoice = false;
    alert = null;
    status = 'Belum terhubung';
    notifyListeners();
  }
}

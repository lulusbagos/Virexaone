import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

enum AppThemeMode {
  midnightCyber, // Mode Malam Gelap Obsidian (Cyan & Emerald)
  solarDay,      // Mode Siang Slate Kontras Tinggi (Anti-Silau, Teks Tegas)
  amberWarmth,   // Mode Industrial Amber (Reduksi Blue-Light, Nyaman di Mata)
  emeraldTactical // Mode Taktis Aviasi / Bio-Phosphor
}

enum AppLanguage {
  id, // Bahasa Indonesia
  en  // English
}

class FmsSettingsService extends ChangeNotifier {
  static final FmsSettingsService _instance = FmsSettingsService._internal();
  factory FmsSettingsService() => _instance;
  FmsSettingsService._internal();

  AppThemeMode _currentTheme = AppThemeMode.midnightCyber;
  AppLanguage _currentLanguage = AppLanguage.id;
  bool _isInitialized = false;

  AppThemeMode get currentTheme => _currentTheme;
  AppLanguage get currentLanguage => _currentLanguage;
  bool get isInitialized => _isInitialized;

  static const String _keyTheme = 'virexa_fms_theme';
  static const String _keyLang = 'virexa_fms_lang';

  Future<void> init() async {
    if (_isInitialized) return;
    try {
      final prefs = await SharedPreferences.getInstance();
      final themeIndex = prefs.getInt(_keyTheme);
      if (themeIndex != null && themeIndex >= 0 && themeIndex < AppThemeMode.values.length) {
        _currentTheme = AppThemeMode.values[themeIndex];
      }

      final langIndex = prefs.getInt(_keyLang);
      if (langIndex != null && langIndex >= 0 && langIndex < AppLanguage.values.length) {
        _currentLanguage = AppLanguage.values[langIndex];
      }
    } catch (_) {}
    _isInitialized = true;
    notifyListeners();
  }

  Future<void> setTheme(AppThemeMode theme) async {
    if (_currentTheme == theme) return;
    _currentTheme = theme;
    notifyListeners();
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setInt(_keyTheme, theme.index);
    } catch (_) {}
  }

  Future<void> setLanguage(AppLanguage language) async {
    if (_currentLanguage == language) return;
    _currentLanguage = language;
    notifyListeners();
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setInt(_keyLang, language.index);
    } catch (_) {}
  }

  // Multi-Language Localization Dictionary
  String tr(String key) {
    final isEn = _currentLanguage == AppLanguage.en;
    if (isEn && _translationsEn.containsKey(key)) {
      return _translationsEn[key]!;
    }
    if (!isEn && _translationsId.containsKey(key)) {
      return _translationsId[key]!;
    }
    return key;
  }

  static const Map<String, String> _translationsId = {
    // Navigation & App Bar
    'app_title': 'VIREXA ONE FMS',
    'cockpit_title': 'COCKPIT IN-CABIN',
    'status_online': 'TERHUBUNG KE SERVER',
    'status_offline': 'OFFLINE (STANDBY)',
    'menu_btn': 'MENU FMS',
    'quick_comms': 'RADIO DISPATCH',
    'refresh': 'SEGARKAN',

    // Dashboard Telemetry
    'speed': 'KECEPATAN',
    'kmh': 'KM/JAM',
    'distance': 'JARAK TUJUAN',
    'meters': 'METER',
    'ritase': 'TOTAL RITASE',
    'trips': 'RIT',
    'payload': 'MUATAN (PAYLOAD)',
    'tons': 'TON',
    'fuel': 'BAHAN BAKAR',
    'engine_temp': 'SUHU MESIN',
    'trans_temp': 'TRANSMISI',
    'tire_temp': 'SUHU BAN',
    'slope_grade': 'KEMIRINGAN JALUR',
    'compass_bearing': 'KOMPAS PIT',
    'target_assignment': 'TARGET PENUGASAN',
    'current_operation': 'STATUS OPERASI SAAT INI',

    // Unit States
    'state_travelling_to_load': 'Menuju Front Gali',
    'state_queueing_load': 'Antre Loading Front',
    'state_loading': 'Sedang Pemuatan (Loading)',
    'state_hauling': 'Hauling Muatan (Mengangkut)',
    'state_queueing_dump': 'Antre di Disposal',
    'state_dumping': 'Dumping / Membuang',
    'state_idle': 'Idle / Standby Siap',
    'state_delay': 'Delay Operasional',
    'state_breakdown': 'Breakdown / Rusak',

    // Menu Tabs
    'tab_operation': 'Operasi Kabin',
    'tab_assignment': 'Penugasan & Rute',
    'tab_fleet': 'Radar Armada',
    'tab_messages': 'Pesan & Radio Comms',
    'tab_locations': 'Geofence Pit',
    'tab_diagnostics': 'Diagnostik ECU',
    'tab_hexagon': 'Integrasi Hexagon',
    'tab_settings': 'Pengaturan & Tema',

    // Settings Page
    'settings_title': 'PENGATURAN TAMPILAN & BAHASA',
    'settings_subtitle': 'Personalisasi antarmuka kabin, kontras siang/malam, dan bahasa sistem',
    'theme_section_title': 'PILIHAN TEMA VISUAL (ERGONOMI MATA)',
    'theme_midnight_title': 'Cyber Midnight (Malam Hari)',
    'theme_midnight_desc': 'Hitam obsidian gelap dengan aksen Cyan & Emerald. Nyaman dan tidak menyilaukan di kabin malam hari.',
    'theme_solar_title': 'Solar Day (Siang Hari)',
    'theme_solar_desc': 'Slate abu-abu gelap dengan kontras tinggi & teks terang. Tahan silau sinar matahari langsung.',
    'theme_amber_title': 'Industrial Amber (Shift Subuh/Malam)',
    'theme_amber_desc': 'Warm Amber & Gold. Mengurangi radiasi cahaya biru dan kelelahan mata operator.',
    'theme_emerald_title': 'Tactical Emerald (Aviasi/Militer)',
    'theme_emerald_desc': 'Hijau fosfor aviasi dengan ketajaman visual tinggi untuk monitoring taktis.',
    
    'lang_section_title': 'PILIHAN BAHASA (LANGUAGE)',
    'lang_id_title': 'Bahasa Indonesia',
    'lang_id_desc': 'Istilah standar operasional pertambangan Indonesia (Front, Disposal, Ritase, P2H, Hauling).',
    'lang_en_title': 'English (International)',
    'lang_en_desc': 'Standard international Fleet Management System terminology.',
    
    // Comms & Radio
    'radio_title': 'RADIO DISPATCH & TALKBACK',
    'radio_channel': 'Kanal: UTAMA • 16.0 kHz PCM16 Half-Duplex',
    'hold_to_talk': 'TEKAN & TAHAN MIC\nUNTUK BICARA KE KONTROL',
    'transmitting_voice': '● TRANSMISI SUARA AKTIF\n(LEPAS UNTUK SELESAI)',
    'receiving_voice': '🔊 RUANG KONTROL SEDANG BERBICARA...',
    'quick_presets': 'BALASAN CEPAT:',
    'type_message': 'Ketik pesan ke ruang kontrol...',
    'send_btn': 'KIRIM',
    'close_btn': 'TUTUP',
  };

  static const Map<String, String> _translationsEn = {
    // Navigation & App Bar
    'app_title': 'VIREXA ONE FMS',
    'cockpit_title': 'IN-CABIN COCKPIT',
    'status_online': 'CONNECTED TO SERVER',
    'status_offline': 'OFFLINE (STANDBY)',
    'menu_btn': 'FMS MENU',
    'quick_comms': 'DISPATCH RADIO',
    'refresh': 'REFRESH',

    // Dashboard Telemetry
    'speed': 'SPEED',
    'kmh': 'KM/H',
    'distance': 'TARGET DISTANCE',
    'meters': 'METERS',
    'ritase': 'TOTAL TRIPS',
    'trips': 'TRIPS',
    'payload': 'PAYLOAD WEIGHT',
    'tons': 'TONS',
    'fuel': 'FUEL LEVEL',
    'engine_temp': 'ENGINE TEMP',
    'trans_temp': 'TRANS TEMP',
    'tire_temp': 'TIRE TEMP',
    'slope_grade': 'HAUL ROAD GRADE',
    'compass_bearing': 'PIT COMPASS',
    'target_assignment': 'TARGET ASSIGNMENT',
    'current_operation': 'CURRENT OPERATIONAL STATE',

    // Unit States
    'state_travelling_to_load': 'Travelling to Loading Front',
    'state_queueing_load': 'Queueing at Front',
    'state_loading': 'Loading Active',
    'state_hauling': 'Hauling Payload',
    'state_queueing_dump': 'Queueing at Dump/Disposal',
    'state_dumping': 'Dumping Material',
    'state_idle': 'Idle / Standby Ready',
    'state_delay': 'Operational Delay',
    'state_breakdown': 'Breakdown / Mechanical',

    // Menu Tabs
    'tab_operation': 'Cabin Ops',
    'tab_assignment': 'Assignment & Route',
    'tab_fleet': 'Fleet Radar',
    'tab_messages': 'Messages & Comms',
    'tab_locations': 'Geofence Pit',
    'tab_diagnostics': 'ECU Diagnostics',
    'tab_hexagon': 'Hexagon Integration',
    'tab_settings': 'Settings & Theme',

    // Settings Page
    'settings_title': 'DISPLAY & LANGUAGE SETTINGS',
    'settings_subtitle': 'Customize cabin theme, day/night contrast, and system language',
    'theme_section_title': 'VISUAL THEME SELECTION (ERGONOMICS)',
    'theme_midnight_title': 'Cyber Midnight (Night Shift)',
    'theme_midnight_desc': 'Deep Obsidian dark with Cyan & Emerald accents. Low-glare protection for night pit ops.',
    'theme_solar_title': 'Solar Day (Daylight High-Contrast)',
    'theme_solar_desc': 'Deep slate navy with crisp bright typography. Anti-glare and high sunlight legibility.',
    'theme_amber_title': 'Industrial Amber (Dawn/Dusk Shift)',
    'theme_amber_desc': 'Warm Amber & Gold. Minimizes blue-light strain and eye fatigue during long shifts.',
    'theme_emerald_title': 'Tactical Emerald (Aviation/Tactical)',
    'theme_emerald_desc': 'Bio-phosphor green with razor-sharp readability for tactical telemetry.',
    
    'lang_section_title': 'SYSTEM LANGUAGE SELECTION',
    'lang_id_title': 'Indonesian (Bahasa Indonesia)',
    'lang_id_desc': 'Standard Indonesian mining terms (Front, Disposal, Ritase, P2H, Hauling).',
    'lang_en_title': 'English (International)',
    'lang_en_desc': 'Standard international Fleet Management System terminology.',
    
    // Comms & Radio
    'radio_title': 'DISPATCH RADIO & TALKBACK',
    'radio_channel': 'Channel: PRIMARY • 16.0 kHz PCM16 Half-Duplex',
    'hold_to_talk': 'HOLD MIC BUTTON\nTO TALK TO DISPATCH',
    'transmitting_voice': '● VOICE TRANSMITTING\n(RELEASE TO FINISH)',
    'receiving_voice': '🔊 CONTROL ROOM IS TRANSMITTING...',
    'quick_presets': 'QUICK PRESETS:',
    'type_message': 'Type message to control room...',
    'send_btn': 'SEND',
    'close_btn': 'CLOSE',
  };
}

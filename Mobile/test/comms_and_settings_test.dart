import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:virexa_mobile/services/fms_settings_service.dart';
import 'package:virexa_mobile/services/live_cabin_comms_service.dart';
import 'package:virexa_mobile/theme/fms_theme.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('FmsSettingsService & FmsTheme Simulation Tests', () {
    final settings = FmsSettingsService();

    test('Theme switching updates FmsTheme adaptive colors properly', () async {
      // 1. Midnight Cyber (Malam Hari)
      await settings.setTheme(AppThemeMode.midnightCyber);
      expect(settings.currentTheme, AppThemeMode.midnightCyber);
      expect(FmsTheme.bgDark, const Color(0xFF030712));
      expect(FmsTheme.emeraldGreen, const Color(0xFF00FFA3));

      // 2. Solar Day (Siang Hari Anti-Silau)
      await settings.setTheme(AppThemeMode.solarDay);
      expect(settings.currentTheme, AppThemeMode.solarDay);
      expect(FmsTheme.bgDark, const Color(0xFF0A121E));
      expect(FmsTheme.cyanAccent, const Color(0xFF00D8F6));

      // 3. Industrial Amber (Anti-Lelah Mata)
      await settings.setTheme(AppThemeMode.amberWarmth);
      expect(settings.currentTheme, AppThemeMode.amberWarmth);
      expect(FmsTheme.bgDark, const Color(0xFF0B0803));
      expect(FmsTheme.emeraldGreen, const Color(0xFFFFB300));

      // 4. Tactical Emerald (Aviasi)
      await settings.setTheme(AppThemeMode.emeraldTactical);
      expect(settings.currentTheme, AppThemeMode.emeraldTactical);
      expect(FmsTheme.bgDark, const Color(0xFF020B07));
      expect(FmsTheme.cyanAccent, const Color(0xFF64FFDA));
    });

    test('Language switching translates keys accurately between ID and EN', () async {
      // Bahasa Indonesia
      await settings.setLanguage(AppLanguage.id);
      expect(settings.currentLanguage, AppLanguage.id);
      expect(settings.tr('speed'), 'KECEPATAN');
      expect(settings.tr('distance'), 'JARAK TUJUAN');
      expect(settings.tr('state_hauling'), 'Hauling Muatan (Mengangkut)');

      // English
      await settings.setLanguage(AppLanguage.en);
      expect(settings.currentLanguage, AppLanguage.en);
      expect(settings.tr('speed'), 'SPEED');
      expect(settings.tr('distance'), 'TARGET DISTANCE');
      expect(settings.tr('state_hauling'), 'Hauling Payload');
    });
  });

  group('LiveCabinCommsService Simulation Tests', () {
    final live = LiveCabinCommsService();

    test('Alert state handling and dismissal simulation', () {
      expect(live.speaking, false);
      expect(live.receivingVoice, false);

      // Simulasikan dismissal
      live.dismissAlert();
      expect(live.alert, isNull);
    });
  });
}

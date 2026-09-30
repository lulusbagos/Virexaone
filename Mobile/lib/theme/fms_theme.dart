import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import '../services/fms_settings_service.dart';

class FmsTheme {
  // Static Constant Color fallbacks (for static const widgets)
  static const Color defaultBgDark = Color(0xFF030712);
  static const Color defaultSurfaceDark = Color(0xFF060E1E);
  static const Color defaultCardBg = Color(0xF208162A);
  static const Color defaultCardHeaderBg = Color(0xF8040D18);
  static const Color defaultCardBorder = Color(0x5500E5FF);
  static const Color defaultCardBorderActive = Color(0xAA00FFA3);

  static const Color defaultCyanAccent = Color(0xFF00E5FF);
  static const Color defaultEmeraldGreen = Color(0xFF00FFA3);
  static const Color amberWarning = Color(0xFFFFB800);
  static const Color redHazard = Color(0xFFFF2A55);
  static const Color blueAction = Color(0xFF0077FE);
  static const Color defaultTextMuted = Color(0xFF7E9BB8);
  static const Color defaultTextLight = Color(0xFFE2EDF8);

  // Dynamic Theme Adaptive Getters
  static AppThemeMode get _mode => FmsSettingsService().currentTheme;

  static Color get bgDark {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const Color(0xFF091322); // Deep Rich Navy (Anti-Glare Sunlight)
      case AppThemeMode.amberWarmth:
        return const Color(0xFF0C0904); // Soothing Warm Espresso Dark (Zero Blue-Light Fatigue)
      case AppThemeMode.emeraldTactical:
        return const Color(0xFF020B07); // Tactical Radar Night Green
      case AppThemeMode.midnightCyber:
        return const Color(0xFF030712); // Deep Obsidian Cyber Dark
    }
  }

  static Color get surfaceDark {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const Color(0xFF0E1C30);
      case AppThemeMode.amberWarmth:
        return const Color(0xFF161006);
      case AppThemeMode.emeraldTactical:
        return const Color(0xFF05170E);
      case AppThemeMode.midnightCyber:
        return const Color(0xFF060E1E);
    }
  }

  static Color get cardBg {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const Color(0xF5142640);
      case AppThemeMode.amberWarmth:
        return const Color(0xF222170A);
      case AppThemeMode.emeraldTactical:
        return const Color(0xF2092215);
      case AppThemeMode.midnightCyber:
        return const Color(0xF208162A);
    }
  }

  static Color get cardHeaderBg {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const Color(0xF80A1526);
      case AppThemeMode.amberWarmth:
        return const Color(0xF8120B03);
      case AppThemeMode.emeraldTactical:
        return const Color(0xF803120A);
      case AppThemeMode.midnightCyber:
        return const Color(0xF8040D18);
    }
  }

  static Color get cardBorder {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const Color(0x8800D4FF);
      case AppThemeMode.amberWarmth:
        return const Color(0x77FFA000);
      case AppThemeMode.emeraldTactical:
        return const Color(0x6600FFA3);
      case AppThemeMode.midnightCyber:
        return const Color(0x5500E5FF);
    }
  }

  static Color get cardBorderActive {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const Color(0xEE00E699);
      case AppThemeMode.amberWarmth:
        return const Color(0xEEFFCA28);
      case AppThemeMode.emeraldTactical:
        return const Color(0xEE64FFDA);
      case AppThemeMode.midnightCyber:
        return const Color(0xAA00FFA3);
    }
  }

  static Color get cyanAccent {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const Color(0xFF00D4FF);
      case AppThemeMode.amberWarmth:
        return const Color(0xFFFFCA28);
      case AppThemeMode.emeraldTactical:
        return const Color(0xFF64FFDA);
      case AppThemeMode.midnightCyber:
        return const Color(0xFF00E5FF);
    }
  }

  static Color get emeraldGreen {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const Color(0xFF00E699);
      case AppThemeMode.amberWarmth:
        return const Color(0xFF81C784);
      case AppThemeMode.emeraldTactical:
        return const Color(0xFF00FFA3);
      case AppThemeMode.midnightCyber:
        return const Color(0xFF00FFA3);
    }
  }

  static Color get textMuted {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const Color(0xFF8DAAC6);
      case AppThemeMode.amberWarmth:
        return const Color(0xFFB8A287);
      case AppThemeMode.emeraldTactical:
        return const Color(0xFF6EA892);
      case AppThemeMode.midnightCyber:
        return const Color(0xFF7E9BB8);
    }
  }

  static Color get textLight {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const Color(0xFFFFFFFF);
      case AppThemeMode.amberWarmth:
        return const Color(0xFFFFF8E7);
      case AppThemeMode.emeraldTactical:
        return const Color(0xFFE0FFF4);
      case AppThemeMode.midnightCyber:
        return const Color(0xFFE2EDF8);
    }
  }

  // Adaptive Gradients
  static LinearGradient get cyberCardGradient {
    switch (_mode) {
      case AppThemeMode.solarDay:
        return const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xF5182942), Color(0xF50F1B2C)],
        );
      case AppThemeMode.amberWarmth:
        return const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xF22B1E0A), Color(0xF2140D04)],
        );
      case AppThemeMode.emeraldTactical:
        return const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xF20F2E1E), Color(0xF204140B)],
        );
      case AppThemeMode.midnightCyber:
        return const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xF00D1F38), Color(0xF0050E1A)],
        );
    }
  }

  static LinearGradient get activeTargetGradient {
    switch (_mode) {
      case AppThemeMode.amberWarmth:
        return const LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0xEE2E2207), Color(0xEE1A1303)],
        );
      case AppThemeMode.solarDay:
      case AppThemeMode.emeraldTactical:
      case AppThemeMode.midnightCyber:
        return const LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0xEE0A241B), Color(0xEE04120D)],
        );
    }
  }

  static List<BoxShadow> neonGlowShadow(Color glowColor, {double opacity = 0.25, double blur = 12}) {
    return [
      BoxShadow(
        color: glowColor.withValues(alpha: opacity),
        blurRadius: blur,
        spreadRadius: 1,
        offset: const Offset(0, 2),
      ),
    ];
  }

  // Modern Google Fonts Typography
  static TextStyle get displayLarge => GoogleFonts.rajdhani(
    fontSize: 32,
    fontWeight: FontWeight.w800,
    color: emeraldGreen,
    letterSpacing: 1.0,
  );

  static TextStyle get displayMedium => GoogleFonts.rajdhani(
    fontSize: 22,
    fontWeight: FontWeight.w700,
    color: cyanAccent,
    letterSpacing: 0.8,
  );

  static TextStyle get titleLarge => GoogleFonts.inter(
    fontSize: 16,
    fontWeight: FontWeight.w700,
    color: textLight,
    letterSpacing: 0.4,
  );

  static TextStyle get titleMedium => GoogleFonts.inter(
    fontSize: 13,
    fontWeight: FontWeight.w700,
    color: textLight,
    letterSpacing: 0.3,
  );

  static TextStyle get bodyNormal => GoogleFonts.inter(
    fontSize: 12,
    fontWeight: FontWeight.w500,
    color: textLight,
  );

  static TextStyle get caption => GoogleFonts.inter(
    fontSize: 10,
    fontWeight: FontWeight.w500,
    color: textMuted,
  );

  static TextStyle get codePill => GoogleFonts.jetBrainsMono(
    fontSize: 11,
    fontWeight: FontWeight.w700,
    color: cyanAccent,
  );
}

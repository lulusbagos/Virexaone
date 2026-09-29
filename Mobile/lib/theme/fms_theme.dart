import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

class FmsTheme {
  // Background & Surface Colors (Deep Cyber Obsidian & Glassmorphism)
  static const Color bgDark = Color(0xFF030712);
  static const Color surfaceDark = Color(0xFF060E1E);
  static const Color cardBg = Color(0xF208162A);
  static const Color cardHeaderBg = Color(0xF8040D18);
  static const Color cardBorder = Color(0x5500E5FF);
  static const Color cardBorderActive = Color(0xAA00FFA3);

  // Luminous Accent Colors
  static const Color cyanAccent = Color(0xFF00E5FF);
  static const Color emeraldGreen = Color(0xFF00FFA3);
  static const Color amberWarning = Color(0xFFFFB800);
  static const Color redHazard = Color(0xFFFF2A55);
  static const Color blueAction = Color(0xFF0077FE);
  static const Color textMuted = Color(0xFF7E9BB8);
  static const Color textLight = Color(0xFFE2EDF8);

  // Gradients for cockpit surfaces
  static const LinearGradient cyberCardGradient = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [Color(0xF00D1F38), Color(0xF0050E1A)],
  );

  static const LinearGradient activeTargetGradient = LinearGradient(
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
    colors: [Color(0xEE0A241B), Color(0xEE04120D)],
  );

  // Glow shadows for futuristic cards
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
    color: Colors.white,
    letterSpacing: 0.4,
  );

  static TextStyle get titleMedium => GoogleFonts.inter(
    fontSize: 13,
    fontWeight: FontWeight.w700,
    color: Colors.white,
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

  static TextStyle get codePill => GoogleFonts.robotoMono(
    fontSize: 11,
    fontWeight: FontWeight.w700,
    color: cyanAccent,
  );
}

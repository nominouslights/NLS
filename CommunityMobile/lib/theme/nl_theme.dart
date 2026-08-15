import 'package:flutter/material.dart';

/// Design tokens for the Community Mobile mockup — transcribed from the
/// supplied 11-screen design board, not from the web apps' theme.ts.
/// The four status hexes are the platform-protected palette and must never
/// change; a status color never appears without an icon and a text label.
abstract class NLColors {
  static const primary = Color(0xFF005493);
  static const navyDark = Color(0xFF0B2239);
  static const navyMid = Color(0xFF123B5E);

  static const gold = Color(0xFFE8A020);
  static const goldLight = Color(0xFFF5B301);
  static const goldTint = Color(0xFFFDF3DC);
  static const goldTextDark = Color(0xFF8A5A00);

  // Protected status hexes.
  static const confirmed = Color(0xFF009E73);
  static const pending = Color(0xFFE1B000);
  static const problem = Color(0xFFD55E00);
  static const unavailable = Color(0xFF4A4A4A);

  // Darkened text variants that keep AA contrast on tinted chip backgrounds.
  static const confirmedText = Color(0xFF007A59);
  static const pendingText = Color(0xFF8A6D00);
  static const problemText = Color(0xFFAD4C00);

  static const pageBg = Color(0xFFF2F5F9);
  static const card = Color(0xFFFFFFFF);
  static const inputBg = Color(0xFFF7F9FC);
  static const border = Color(0xFFE1E8F0);
  static const borderStrong = Color(0xFFC7D3E0);

  static const textPrimary = Color(0xFF13293D);
  static const textMuted = Color(0xFF5A7184);
  static const textFaintOnDark = Color(0xFFAABDD1);
}

abstract class NLRadii {
  static const double card = 14;
  static const double button = 10;
  static const double chip = 8;
}

abstract class NLFonts {
  static const body = 'Barlow';
  static const condensed = 'Barlow Condensed';
  static const semiCondensed = 'Barlow Semi Condensed';
}

abstract class NLText {
  static const screenTitle = TextStyle(
    fontFamily: NLFonts.condensed,
    fontSize: 22,
    fontWeight: FontWeight.w700,
    color: NLColors.textPrimary,
    height: 1.1,
  );

  static const sectionLabel = TextStyle(
    fontFamily: NLFonts.semiCondensed,
    fontSize: 12,
    fontWeight: FontWeight.w600,
    color: NLColors.textMuted,
    letterSpacing: 1.4,
  );

  static const cardTitle = TextStyle(
    fontFamily: NLFonts.body,
    fontSize: 15.5,
    fontWeight: FontWeight.w600,
    color: NLColors.textPrimary,
  );

  static const body = TextStyle(
    fontFamily: NLFonts.body,
    fontSize: 13.5,
    color: NLColors.textPrimary,
    height: 1.35,
  );

  static const muted = TextStyle(
    fontFamily: NLFonts.body,
    fontSize: 12.5,
    color: NLColors.textMuted,
    height: 1.35,
  );

  static const bigNumber = TextStyle(
    fontFamily: NLFonts.condensed,
    fontSize: 34,
    fontWeight: FontWeight.w700,
    color: NLColors.textPrimary,
    height: 1,
    fontFeatures: [FontFeature.tabularFigures()],
  );
}

ThemeData buildNlTheme() {
  final base = ThemeData(
    useMaterial3: true,
    fontFamily: NLFonts.body,
    scaffoldBackgroundColor: NLColors.pageBg,
    colorScheme: ColorScheme.fromSeed(
      seedColor: NLColors.primary,
      primary: NLColors.primary,
      surface: NLColors.card,
    ),
  );
  return base.copyWith(
    appBarTheme: const AppBarTheme(
      backgroundColor: NLColors.pageBg,
      foregroundColor: NLColors.textPrimary,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: true,
      titleTextStyle: TextStyle(
        fontFamily: NLFonts.body,
        fontSize: 16,
        fontWeight: FontWeight.w600,
        color: NLColors.textPrimary,
      ),
    ),
    dividerTheme: const DividerThemeData(color: NLColors.border, thickness: 1),
    splashFactory: InkSparkle.splashFactory,
  );
}

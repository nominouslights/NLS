import 'package:flutter/material.dart';

import '../theme/nl_theme.dart';

enum NLButtonKind { primary, gold, outline }

class NLButton extends StatelessWidget {
  final String label;
  final NLButtonKind kind;
  final IconData? icon;
  final VoidCallback? onPressed;
  final bool expand;

  const NLButton({
    super.key,
    required this.label,
    this.kind = NLButtonKind.primary,
    this.icon,
    this.onPressed,
    this.expand = true,
  });

  const NLButton.gold({super.key, required this.label, this.icon, this.onPressed, this.expand = true})
      : kind = NLButtonKind.gold;

  const NLButton.outline({super.key, required this.label, this.icon, this.onPressed, this.expand = true})
      : kind = NLButtonKind.outline;

  @override
  Widget build(BuildContext context) {
    final (bg, fg, side) = switch (kind) {
      // Gold surfaces always carry navy text — white on #E8A020 fails AA.
      NLButtonKind.gold => (NLColors.gold, NLColors.navyDark, BorderSide.none),
      NLButtonKind.primary => (NLColors.primary, Colors.white, BorderSide.none),
      NLButtonKind.outline => (
          Colors.transparent,
          NLColors.primary,
          const BorderSide(color: NLColors.borderStrong)
        ),
    };

    final style = ElevatedButton.styleFrom(
      backgroundColor: bg,
      foregroundColor: fg,
      elevation: 0,
      side: side == BorderSide.none ? null : side,
      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 13),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(NLRadii.button)),
      textStyle: const TextStyle(
        fontFamily: NLFonts.body,
        fontSize: 14.5,
        fontWeight: FontWeight.w700,
        letterSpacing: .2,
      ),
    );

    final child = icon == null
        ? Text(label)
        : Row(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [Icon(icon, size: 18), const SizedBox(width: 8), Text(label)],
          );

    final button = ElevatedButton(onPressed: onPressed ?? () {}, style: style, child: child);
    return expand ? SizedBox(width: double.infinity, child: button) : button;
  }
}

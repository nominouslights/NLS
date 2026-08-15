import 'package:flutter/material.dart';

import '../theme/nl_theme.dart';

/// The board's base surface: white rounded card with a soft navy-tinted shadow.
class SectionCard extends StatelessWidget {
  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color color;
  final VoidCallback? onTap;

  const SectionCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.color = NLColors.card,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final card = Container(
      width: double.infinity,
      padding: padding,
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(NLRadii.card),
        border: Border.all(color: NLColors.border),
        boxShadow: [
          BoxShadow(
            color: NLColors.navyDark.withValues(alpha: .06),
            blurRadius: 10,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: child,
    );
    if (onTap == null) return card;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(NLRadii.card),
        child: card,
      ),
    );
  }
}

/// Uppercase section eyebrow ("RECENT ACTIVITY", "TRIP INFO").
class SectionLabel extends StatelessWidget {
  final String text;

  const SectionLabel(this.text, {super.key});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Text(text.toUpperCase(), style: NLText.sectionLabel),
    );
  }
}

/// Icon on a tinted circle — the board's placeholder for photos/illustrations.
class IconDisc extends StatelessWidget {
  final IconData icon;
  final Color color;
  final double size;

  const IconDisc(this.icon, {super.key, this.color = NLColors.primary, this.size = 40});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(color: color.withValues(alpha: .12), shape: BoxShape.circle),
      child: Icon(icon, size: size * .5, color: color),
    );
  }
}

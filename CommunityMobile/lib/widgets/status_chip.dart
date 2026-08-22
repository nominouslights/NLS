import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';

/// Platform non-negotiable: a status color never stands alone — every chip is
/// color + icon + text label. Same construction as Dispatcher's StatusChip:
/// tinted pill, solid rounded-square glyph in the raw status color, darkened
/// AA text variant for the label.
class StatusChip extends StatelessWidget {
  final Color color;
  final Color textColor;
  final IconData icon;
  final String label;
  final bool onDark;

  const StatusChip({
    super.key,
    required this.color,
    required this.textColor,
    required this.icon,
    required this.label,
    this.onDark = false,
  });

  factory StatusChip.forTrip(TripStatus status, {bool onDark = false}) {
    switch (status) {
      case TripStatus.confirmed:
        return StatusChip(
            color: NLColors.confirmed,
            textColor: NLColors.confirmedText,
            icon: Icons.check,
            label: 'Confirmed',
            onDark: onDark);
      case TripStatus.booked:
        return StatusChip(
            color: NLColors.primary,
            textColor: NLColors.primary,
            icon: Icons.event_available,
            label: 'Booked',
            onDark: onDark);
      case TripStatus.pending:
        return StatusChip(
            color: NLColors.pending,
            textColor: NLColors.pendingText,
            icon: Icons.hourglass_bottom,
            label: 'Pending',
            onDark: onDark);
      case TripStatus.cancelled:
        return StatusChip(
            color: NLColors.problem,
            textColor: NLColors.problemText,
            icon: Icons.close,
            label: 'Cancelled',
            onDark: onDark);
    }
  }

  @override
  Widget build(BuildContext context) {
    final glyphOnColor =
        color == NLColors.pending ? NLColors.navyDark : Colors.white;
    return Container(
      padding: const EdgeInsets.fromLTRB(4, 4, 10, 4),
      decoration: BoxDecoration(
        color: onDark ? Colors.white.withValues(alpha: .14) : color.withValues(alpha: .12),
        borderRadius: BorderRadius.circular(NLRadii.chip),
        border: Border.all(
            color: onDark ? Colors.white.withValues(alpha: .25) : color.withValues(alpha: .4)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 18,
            height: 18,
            decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(5)),
            child: Icon(icon, size: 12, color: glyphOnColor),
          ),
          const SizedBox(width: 6),
          Text(
            label,
            style: TextStyle(
              fontFamily: NLFonts.body,
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: onDark ? Colors.white : textColor,
            ),
          ),
        ],
      ),
    );
  }
}

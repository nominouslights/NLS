import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';

/// "+150 pts" in teal, "−500 pts" in vermillion — never raw red/green.
class PointsDelta extends StatelessWidget {
  final int points;

  const PointsDelta(this.points, {super.key});

  @override
  Widget build(BuildContext context) {
    final negative = points < 0;
    return Text(
      '${negative ? '−' : '+'}${formatPoints(points)} pts',
      style: TextStyle(
        fontFamily: NLFonts.body,
        fontSize: 13.5,
        fontWeight: FontWeight.w700,
        color: negative ? NLColors.problemText : NLColors.confirmedText,
      ),
    );
  }
}

/// Gold points cost, e.g. "2,500 pts" on reward rows.
class PointsCost extends StatelessWidget {
  final int points;
  final double fontSize;
  final bool onDark;

  const PointsCost(this.points, {super.key, this.fontSize = 13.5, this.onDark = false});

  @override
  Widget build(BuildContext context) {
    return Text(
      '${formatPoints(points)} pts',
      style: TextStyle(
        fontFamily: NLFonts.condensed,
        fontSize: fontSize,
        fontWeight: FontWeight.w700,
        color: onDark ? NLColors.goldLight : NLColors.goldTextDark,
      ),
    );
  }
}

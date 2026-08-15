import 'package:flutter/material.dart';

import '../theme/nl_theme.dart';

/// Rounded progress track used by Community Impact cards and the profile
/// level bar.
class NLProgressBar extends StatelessWidget {
  final double value; // 0..1
  final Color color;
  final double height;

  const NLProgressBar(this.value, {super.key, this.color = NLColors.primary, this.height = 8});

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(height),
      child: LinearProgressIndicator(
        value: value.clamp(0, 1),
        minHeight: height,
        backgroundColor: NLColors.border,
        valueColor: AlwaysStoppedAnimation(color),
      ),
    );
  }
}

/// "Trip Progress" row: filled teal riders, hollow gray placeholders, and the
/// "2 / 4 minimum" caption. Icon + label accompany the color per the
/// status-color rule.
class RiderProgress extends StatelessWidget {
  final int joined;
  final int minimum;
  final int totalSeats;

  const RiderProgress({super.key, required this.joined, required this.minimum, this.totalSeats = 8});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            for (var i = 0; i < totalSeats; i++)
              Padding(
                padding: const EdgeInsets.only(right: 6),
                child: Icon(
                  i < joined ? Icons.person : Icons.person_outline,
                  size: 24,
                  color: i < joined
                      ? NLColors.confirmed
                      : i < minimum
                          ? NLColors.textMuted
                          : NLColors.border,
                ),
              ),
          ],
        ),
        const SizedBox(height: 6),
        Text('$joined / $minimum minimum', style: NLText.muted),
      ],
    );
  }
}

import 'package:flutter/material.dart';

import '../theme/nl_theme.dart';

/// Pill segmented control: Upcoming/Past/Cancelled, Active/Past Projects,
/// Cargo/Groceries.
class SegmentedTabs extends StatelessWidget {
  final List<String> labels;
  final int selected;
  final ValueChanged<int> onChanged;

  const SegmentedTabs({
    super.key,
    required this.labels,
    required this.selected,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: NLColors.card,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: NLColors.border),
      ),
      child: Row(
        children: [
          for (var i = 0; i < labels.length; i++)
            Expanded(
              child: GestureDetector(
                onTap: () => onChanged(i),
                child: AnimatedContainer(
                  duration: const Duration(milliseconds: 150),
                  padding: const EdgeInsets.symmetric(vertical: 9),
                  decoration: BoxDecoration(
                    color: i == selected ? NLColors.primary : Colors.transparent,
                    borderRadius: BorderRadius.circular(9),
                  ),
                  alignment: Alignment.center,
                  child: Text(
                    labels[i],
                    style: TextStyle(
                      fontFamily: NLFonts.body,
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: i == selected ? Colors.white : NLColors.textMuted,
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

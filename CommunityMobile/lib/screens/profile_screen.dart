import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';
import '../widgets/nl_button.dart';
import '../widgets/progress_bar.dart';
import '../widgets/section_card.dart';

/// Screen 11 — Profile & Badges.
class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  static const _badges = <(IconData, Color)>[
    (Icons.terrain, NLColors.primary),
    (Icons.ac_unit, NLColors.navyDark),
    (Icons.emoji_events, NLColors.gold),
    (Icons.group, NLColors.primary),
    (Icons.star, NLColors.gold),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('My Profile')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
        children: [
          Center(
            child: Column(
              children: [
                const CircleAvatar(
                  radius: 36,
                  backgroundColor: NLColors.primary,
                  child: Text(
                    userInitials,
                    style: TextStyle(
                        fontFamily: NLFonts.body,
                        fontSize: 22,
                        fontWeight: FontWeight.w700,
                        color: Colors.white),
                  ),
                ),
                const SizedBox(height: 10),
                Text(userName, style: NLText.screenTitle),
                const SizedBox(height: 2),
                Text('Level $userLevel – $userLevelName',
                    style: NLText.muted.copyWith(fontWeight: FontWeight.w600)),
              ],
            ),
          ),
          const SizedBox(height: 14),
          SectionCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const NLProgressBar(.75, color: NLColors.gold),
                const SizedBox(height: 8),
                Text('${formatPoints(pointsToNextLevel)} pts to next level',
                    style: NLText.muted),
              ],
            ),
          ),
          const SizedBox(height: 18),
          const SectionLabel('My Stats'),
          SectionCard(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
            child: Column(
              children: [
                for (var i = 0; i < userStats.length; i++) ...[
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 11),
                    child: Row(
                      children: [
                        Expanded(child: Text(userStats[i].$1, style: NLText.body)),
                        Text(userStats[i].$2,
                            style: NLText.body.copyWith(fontWeight: FontWeight.w700)),
                      ],
                    ),
                  ),
                  if (i < userStats.length - 1) const Divider(height: 1),
                ],
              ],
            ),
          ),
          const SizedBox(height: 18),
          const SectionLabel('Badges'),
          Row(
            children: [
              for (final (icon, color) in _badges)
                Padding(
                  padding: const EdgeInsets.only(right: 12),
                  child: Container(
                    width: 48,
                    height: 48,
                    decoration: BoxDecoration(
                      color: color,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: NLColors.border, width: 2),
                    ),
                    child: Icon(icon, size: 24, color: Colors.white),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 18),
          NLButton.gold(label: 'View All Badges', onPressed: () {}),
        ],
      ),
    );
  }
}

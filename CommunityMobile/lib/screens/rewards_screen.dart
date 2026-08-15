import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';
import '../widgets/nl_button.dart';
import '../widgets/points_text.dart';
import '../widgets/section_card.dart';
import 'gift_basket_screen.dart';

/// Screen 7 — Rewards Corner.
class RewardsScreen extends StatefulWidget {
  const RewardsScreen({super.key});

  @override
  State<RewardsScreen> createState() => _RewardsScreenState();
}

class _RewardsScreenState extends State<RewardsScreen> {
  String _category = 'All';

  @override
  Widget build(BuildContext context) {
    final items = _category == 'All'
        ? rewards
        : rewards.where((r) => r.category == _category).toList();
    return Scaffold(
      appBar: AppBar(title: const Text('Rewards Corner')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
        children: [
          Center(
            child: Text.rich(
              TextSpan(
                text: 'Your Points:  ',
                style: NLText.body,
                children: [
                  TextSpan(
                    text: '${formatPoints(userPoints)} pts',
                    style: NLText.body.copyWith(
                        fontWeight: FontWeight.w700, color: NLColors.goldTextDark),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 36,
            child: ListView(
              scrollDirection: Axis.horizontal,
              children: [
                for (final c in rewardCategories)
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text(c),
                      selected: _category == c,
                      onSelected: (_) => setState(() => _category = c),
                      selectedColor: NLColors.primary,
                      backgroundColor: NLColors.card,
                      side: const BorderSide(color: NLColors.border),
                      showCheckmark: false,
                      labelStyle: TextStyle(
                        fontFamily: NLFonts.body,
                        fontSize: 12.5,
                        fontWeight: FontWeight.w600,
                        color: _category == c ? Colors.white : NLColors.textMuted,
                      ),
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(height: 14),
          if (items.isEmpty)
            SectionCard(
              padding: const EdgeInsets.symmetric(vertical: 32),
              child: Column(
                children: [
                  const Icon(Icons.card_giftcard, size: 34, color: NLColors.textMuted),
                  const SizedBox(height: 8),
                  Text('More rewards coming soon.',
                      style: NLText.muted, textAlign: TextAlign.center),
                ],
              ),
            )
          else
            for (final r in items) ...[
              _rewardRow(context, r),
              const SizedBox(height: 10),
            ],
          const SizedBox(height: 8),
          NLButton.gold(label: 'View All Rewards', onPressed: () {}),
        ],
      ),
    );
  }

  Widget _rewardRow(BuildContext context, Reward r) {
    return SectionCard(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      onTap: r.includes.isEmpty
          ? () {}
          : () => Navigator.push(
              context, MaterialPageRoute(builder: (_) => GiftBasketScreen(reward: r))),
      child: Row(
        children: [
          Container(
            width: 52,
            height: 52,
            decoration: BoxDecoration(
              color: NLColors.navyDark,
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(r.icon, size: 26, color: NLColors.goldLight),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(r.name, style: NLText.body.copyWith(fontWeight: FontWeight.w600)),
                const SizedBox(height: 2),
                PointsCost(r.points, fontSize: 15),
                Text('Quantity: ${r.quantity}', style: NLText.muted.copyWith(fontSize: 11.5)),
              ],
            ),
          ),
          const Icon(Icons.chevron_right, color: NLColors.textMuted),
        ],
      ),
    );
  }
}

import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';
import '../widgets/nl_button.dart';
import '../widgets/points_text.dart';
import '../widgets/section_card.dart';

/// Screen 5 — My Wallet & Points (shell tab).
class WalletScreen extends StatelessWidget {
  const WalletScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: [
          Center(child: Text('My Wallet', style: NLText.screenTitle)),
          const SizedBox(height: 14),
          _balanceCard(),
          const SizedBox(height: 18),
          const SectionLabel('Recent Activity'),
          SectionCard(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
            child: Column(
              children: [
                for (var i = 0; i < walletActivity.length; i++) ...[
                  _activityRow(walletActivity[i]),
                  if (i < walletActivity.length - 1) const Divider(height: 1),
                ],
              ],
            ),
          ),
          const SizedBox(height: 16),
          NLButton.gold(label: 'View All Activity', onPressed: () {}),
        ],
      ),
    );
  }

  Widget _balanceCard() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [NLColors.goldLight, NLColors.gold],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(NLRadii.card),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('My Points Balance',
                    style: NLText.body.copyWith(
                        color: NLColors.navyDark, fontWeight: FontWeight.w600)),
                const SizedBox(height: 6),
                Text('${formatPoints(userPoints)} pts',
                    style: NLText.bigNumber.copyWith(color: NLColors.navyDark)),
                const SizedBox(height: 10),
                Text('Learn how to earn points  ›',
                    style: NLText.muted.copyWith(
                        color: NLColors.navyDark, fontWeight: FontWeight.w600)),
              ],
            ),
          ),
          Icon(Icons.savings_outlined,
              size: 56, color: NLColors.navyDark.withValues(alpha: .55)),
        ],
      ),
    );
  }

  Widget _activityRow(WalletActivity a) {
    final negative = a.points < 0;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 12),
      child: Row(
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: (negative ? NLColors.problem : NLColors.primary).withValues(alpha: .1),
              shape: BoxShape.circle,
            ),
            child: Icon(a.icon,
                size: 18, color: negative ? NLColors.problemText : NLColors.primary),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(a.label, style: NLText.body.copyWith(fontWeight: FontWeight.w600)),
                const SizedBox(height: 2),
                Text(a.date, style: NLText.muted.copyWith(fontSize: 11.5)),
              ],
            ),
          ),
          PointsDelta(a.points),
        ],
      ),
    );
  }
}

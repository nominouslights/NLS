import 'package:flutter/material.dart';

import '../theme/nl_theme.dart';
import '../widgets/section_card.dart';
import 'book_trip_screen.dart';
import 'cargo_groceries_screen.dart';
import 'invite_earn_screen.dart';
import 'profile_screen.dart';
import 'rewards_screen.dart';

/// Shell tab 5 — "More". Not on the board; a thin list so every designed
/// screen stays reachable.
class MoreScreen extends StatelessWidget {
  const MoreScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final entries = <(IconData, String, Widget)>[
      (Icons.confirmation_number_outlined, 'Book a Trip', const BookTripScreen()),
      (Icons.card_giftcard_outlined, 'Rewards Corner', const RewardsScreen()),
      (Icons.group_add_outlined, 'Invite & Earn', const InviteEarnScreen()),
      (Icons.shopping_basket_outlined, 'Cargo & Groceries', const CargoGroceriesScreen()),
      (Icons.person_outline, 'My Profile', const ProfileScreen()),
    ];
    return SafeArea(
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: [
          Center(child: Text('More', style: NLText.screenTitle)),
          const SizedBox(height: 14),
          for (final (icon, label, screen) in entries) ...[
            SectionCard(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              onTap: () =>
                  Navigator.push(context, MaterialPageRoute(builder: (_) => screen)),
              child: Row(
                children: [
                  IconDisc(icon, size: 38),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(label,
                        style: NLText.body.copyWith(fontWeight: FontWeight.w600)),
                  ),
                  const Icon(Icons.chevron_right, color: NLColors.textMuted),
                ],
              ),
            ),
            const SizedBox(height: 10),
          ],
        ],
      ),
    );
  }
}

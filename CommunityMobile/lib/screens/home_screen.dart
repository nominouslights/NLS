import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';
import '../widgets/nl_button.dart';
import '../widgets/route_text.dart';
import '../widgets/section_card.dart';
import '../widgets/status_chip.dart';
import 'book_trip_screen.dart';
import 'cargo_groceries_screen.dart';
import 'invite_earn_screen.dart';
import 'profile_screen.dart';
import 'rewards_screen.dart';
import 'trip_details_screen.dart';

/// Screen 1 — Home Dashboard.
class HomeScreen extends StatelessWidget {
  /// Lets quick actions jump to another shell tab (My Trips, Impact).
  final ValueChanged<int>? onNavigateTab;

  const HomeScreen({super.key, this.onNavigateTab});

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: [
          _header(context),
          const SizedBox(height: 16),
          const SectionLabel('Your next trip'),
          _nextTripCard(context),
          const SizedBox(height: 14),
          _ridersNeededCard(context),
          const SizedBox(height: 18),
          _quickActions(context),
        ],
      ),
    );
  }

  Widget _header(BuildContext context) {
    return Row(
      children: [
        GestureDetector(
          onTap: () => Navigator.push(
              context, MaterialPageRoute(builder: (_) => const ProfileScreen())),
          child: const CircleAvatar(
            radius: 22,
            backgroundColor: NLColors.primary,
            child: Text(
              userInitials,
              style: TextStyle(
                  fontFamily: NLFonts.body, fontWeight: FontWeight.w700, color: Colors.white),
            ),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('Welcome back,', style: NLText.muted),
              Text('$userFirstName!',
                  style: NLText.screenTitle.copyWith(fontSize: 24)),
            ],
          ),
        ),
        IconButton(
          onPressed: () {},
          icon: const Icon(Icons.notifications_none, color: NLColors.textPrimary),
        ),
      ],
    );
  }

  Widget _nextTripCard(BuildContext context) {
    return SectionCard(
      color: NLColors.navyDark,
      onTap: () => Navigator.push(
          context, MaterialPageRoute(builder: (_) => const TripDetailsScreen(trip: nextTrip))),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: RouteText(
                  from: nextTrip.from,
                  to: nextTrip.to,
                  style: NLText.cardTitle.copyWith(color: Colors.white, fontSize: 17),
                ),
              ),
              StatusChip.forTrip(nextTrip.status, onDark: true),
            ],
          ),
          const SizedBox(height: 8),
          Text('${nextTrip.date}  ·  ${nextTrip.time}',
              style: NLText.body.copyWith(color: Colors.white)),
          const SizedBox(height: 4),
          Text('Booking ID: ${nextTrip.bookingId}',
              style: NLText.muted.copyWith(color: NLColors.textFaintOnDark)),
        ],
      ),
    );
  }

  Widget _ridersNeededCard(BuildContext context) {
    return SectionCard(
      color: NLColors.goldTint,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('This trip needs ${nextTrip.ridersNeeded} more riders',
              style: NLText.cardTitle),
          const SizedBox(height: 2),
          Text('Minimum ${nextTrip.ridersMinimum} riders to go', style: NLText.muted),
          const SizedBox(height: 12),
          Row(
            children: [
              for (var i = 0; i < nextTrip.ridersMinimum; i++)
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: Icon(
                    i < nextTrip.ridersJoined ? Icons.person : Icons.person_outline,
                    size: 30,
                    color: i < nextTrip.ridersJoined ? NLColors.gold : NLColors.textMuted,
                  ),
                ),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              const Icon(Icons.warning_amber_rounded, size: 16, color: NLColors.pendingText),
              const SizedBox(width: 6),
              Text('${nextTrip.ridersNeeded} more riders needed',
                  style: NLText.muted.copyWith(
                      color: NLColors.pendingText, fontWeight: FontWeight.w600)),
            ],
          ),
          const SizedBox(height: 12),
          NLButton.gold(
            label: 'Invite & Earn Points',
            icon: Icons.group_add_outlined,
            onPressed: () => Navigator.push(
                context, MaterialPageRoute(builder: (_) => const InviteEarnScreen())),
          ),
        ],
      ),
    );
  }

  Widget _quickActions(BuildContext context) {
    final actions = <(String, IconData, VoidCallback)>[
      (
        'Book a Trip',
        Icons.confirmation_number_outlined,
        () => Navigator.push(context, MaterialPageRoute(builder: (_) => const BookTripScreen()))
      ),
      ('My Trips', Icons.directions_bus_outlined, () => onNavigateTab?.call(1)),
      (
        'Cargo & Groceries',
        Icons.shopping_basket_outlined,
        () => Navigator.push(
            context, MaterialPageRoute(builder: (_) => const CargoGroceriesScreen()))
      ),
      ('Community Impact', Icons.favorite_outline, () => onNavigateTab?.call(3)),
      (
        'Rewards Corner',
        Icons.card_giftcard_outlined,
        () => Navigator.push(context, MaterialPageRoute(builder: (_) => const RewardsScreen()))
      ),
      (
        'My Profile',
        Icons.person_outline,
        () => Navigator.push(context, MaterialPageRoute(builder: (_) => const ProfileScreen()))
      ),
    ];
    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      childAspectRatio: 1.9,
      children: [
        for (final (label, icon, onTap) in actions)
          SectionCard(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
            onTap: () => onTap(),
            child: Row(
              children: [
                IconDisc(icon, size: 36),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(label,
                      style: NLText.body.copyWith(fontWeight: FontWeight.w600, fontSize: 13)),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

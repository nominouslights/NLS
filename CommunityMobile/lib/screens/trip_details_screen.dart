import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';
import '../widgets/progress_bar.dart';
import '../widgets/route_text.dart';
import '../widgets/section_card.dart';

/// Screen 3 — Trip Details.
class TripDetailsScreen extends StatelessWidget {
  final Trip trip;

  const TripDetailsScreen({super.key, required this.trip});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Trip Details')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
        children: [
          if (trip.status == TripStatus.confirmed) _confirmedBanner(),
          const SizedBox(height: 12),
          SectionCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                RouteText(
                    from: trip.from,
                    to: trip.to,
                    style: NLText.cardTitle.copyWith(fontSize: 17)),
                const SizedBox(height: 4),
                Text('${trip.date}  ·  ${trip.time}', style: NLText.body),
                const SizedBox(height: 4),
                Text('Booking ID: ${trip.bookingId}', style: NLText.muted),
              ],
            ),
          ),
          const SizedBox(height: 14),
          if (trip.ridersNeeded > 0) _progressCard(),
          const SizedBox(height: 14),
          _tripInfoCard(),
        ],
      ),
    );
  }

  Widget _confirmedBanner() {
    // Status rule: teal + check icon + label, never color alone.
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: NLColors.confirmed,
        borderRadius: BorderRadius.circular(NLRadii.button),
      ),
      child: const Row(
        children: [
          Icon(Icons.check_circle, color: Colors.white, size: 22),
          SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text("You're all set!",
                    style: TextStyle(
                        fontFamily: NLFonts.body,
                        fontSize: 14.5,
                        fontWeight: FontWeight.w700,
                        color: Colors.white)),
                Text('This trip is confirmed.',
                    style: TextStyle(
                        fontFamily: NLFonts.body, fontSize: 12.5, color: Colors.white)),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _progressCard() {
    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Trip Progress', style: NLText.cardTitle),
          const SizedBox(height: 2),
          Text('${trip.ridersNeeded} more riders to go', style: NLText.muted),
          const SizedBox(height: 12),
          RiderProgress(joined: trip.ridersJoined, minimum: trip.ridersMinimum),
          const SizedBox(height: 14),
          const Divider(height: 1),
          const SizedBox(height: 12),
          Text('Share to help fill this trip and earn points!',
              style: NLText.body.copyWith(fontWeight: FontWeight.w600)),
          const SizedBox(height: 12),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: const [
              _ShareAction(icon: Icons.link, label: 'Copy Link'),
              _ShareAction(icon: Icons.sms_outlined, label: 'Text'),
              _ShareAction(icon: Icons.facebook, label: 'Facebook'),
              _ShareAction(icon: Icons.chat_bubble_outline, label: 'WhatsApp'),
            ],
          ),
        ],
      ),
    );
  }

  Widget _tripInfoCard() {
    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SectionLabel('Trip Info'),
          _infoRow(Icons.trip_origin, 'Pickup', trip.pickupPoint, trip.pickupTime),
          const SizedBox(height: 12),
          _infoRow(Icons.location_on_outlined, 'Drop off', trip.dropoffPoint, trip.dropoffTime),
          const SizedBox(height: 12),
          _infoRow(Icons.luggage_outlined, "What's Allowed", trip.allowance, ''),
        ],
      ),
    );
  }

  Widget _infoRow(IconData icon, String label, String value, String time) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 18, color: NLColors.primary),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label, style: NLText.muted.copyWith(fontSize: 11.5)),
              const SizedBox(height: 2),
              Text(value, style: NLText.body.copyWith(fontWeight: FontWeight.w600)),
            ],
          ),
        ),
        if (time.isNotEmpty)
          Text(time, style: NLText.body.copyWith(fontWeight: FontWeight.w600)),
      ],
    );
  }
}

class _ShareAction extends StatelessWidget {
  final IconData icon;
  final String label;

  const _ShareAction({required this.icon, required this.label});

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Container(
          width: 48,
          height: 48,
          decoration: BoxDecoration(
            color: NLColors.primary.withValues(alpha: .08),
            shape: BoxShape.circle,
          ),
          child: Icon(icon, size: 22, color: NLColors.primary),
        ),
        const SizedBox(height: 6),
        Text(label, style: NLText.muted.copyWith(fontSize: 11)),
      ],
    );
  }
}

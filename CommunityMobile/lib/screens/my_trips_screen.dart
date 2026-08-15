import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';
import '../widgets/route_text.dart';
import '../widgets/section_card.dart';
import '../widgets/segmented_tabs.dart';
import '../widgets/status_chip.dart';
import 'trip_details_screen.dart';

/// Screen 4 — My Trips (shell tab).
class MyTripsScreen extends StatefulWidget {
  const MyTripsScreen({super.key});

  @override
  State<MyTripsScreen> createState() => _MyTripsScreenState();
}

class _MyTripsScreenState extends State<MyTripsScreen> {
  int _tab = 0;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: [
          Center(child: Text('My Trips', style: NLText.screenTitle)),
          const SizedBox(height: 14),
          SegmentedTabs(
            labels: const ['Upcoming', 'Past', 'Cancelled'],
            selected: _tab,
            onChanged: (i) => setState(() => _tab = i),
          ),
          const SizedBox(height: 14),
          if (_tab == 0) ...[
            for (final t in upcomingTrips) ...[
              _tripCard(context, t),
              const SizedBox(height: 10),
            ],
          ] else
            _emptyState(_tab == 1 ? 'No past trips yet.' : 'No cancelled trips.'),
          const SizedBox(height: 8),
          _supportCard(),
        ],
      ),
    );
  }

  Widget _tripCard(BuildContext context, Trip t) {
    return SectionCard(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      onTap: () => Navigator.push(
          context, MaterialPageRoute(builder: (_) => TripDetailsScreen(trip: t))),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('${t.date}  ·  ${t.time}', style: NLText.muted),
                const SizedBox(height: 4),
                RouteText(
                    from: t.from,
                    to: t.to,
                    style: NLText.body.copyWith(fontWeight: FontWeight.w700, fontSize: 15)),
                const SizedBox(height: 6),
                Text('Booking ID: ${t.bookingId}', style: NLText.muted),
              ],
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              const Icon(Icons.more_vert, size: 18, color: NLColors.textMuted),
              const SizedBox(height: 14),
              StatusChip.forTrip(t.status),
            ],
          ),
        ],
      ),
    );
  }

  Widget _emptyState(String message) {
    return SectionCard(
      padding: const EdgeInsets.symmetric(vertical: 32),
      child: Column(
        children: [
          const Icon(Icons.event_busy_outlined, size: 34, color: NLColors.textMuted),
          const SizedBox(height: 8),
          Text(message, style: NLText.muted, textAlign: TextAlign.center),
        ],
      ),
    );
  }

  Widget _supportCard() {
    return SectionCard(
      child: Row(
        children: [
          const IconDisc(Icons.headset_mic_outlined, size: 42),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Need Help?', style: NLText.cardTitle),
                Text('Contact Support', style: NLText.muted),
              ],
            ),
          ),
          const Icon(Icons.chevron_right, color: NLColors.textMuted),
        ],
      ),
    );
  }
}

import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';
import '../widgets/nl_button.dart';
import '../widgets/route_text.dart';
import '../widgets/section_card.dart';

/// Screen 2 — Book a Trip.
class BookTripScreen extends StatefulWidget {
  const BookTripScreen({super.key});

  @override
  State<BookTripScreen> createState() => _BookTripScreenState();
}

class _BookTripScreenState extends State<BookTripScreen> {
  int _passengers = 1;
  bool _swapped = false;

  @override
  Widget build(BuildContext context) {
    final from = _swapped ? 'Thompson' : 'Leaf Rapids';
    final to = _swapped ? 'Leaf Rapids' : 'Thompson';
    return Scaffold(
      appBar: AppBar(title: const Text('Where are you going?')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        children: [
          SectionCard(
            child: Column(
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Column(
                        children: [
                          _field('From', from),
                          const SizedBox(height: 10),
                          _field('To', to),
                        ],
                      ),
                    ),
                    const SizedBox(width: 10),
                    IconButton(
                      onPressed: () => setState(() => _swapped = !_swapped),
                      icon: const Icon(Icons.swap_vert, color: NLColors.primary),
                      style: IconButton.styleFrom(
                        backgroundColor: NLColors.primary.withValues(alpha: .08),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                _field('Date', 'Fri, Nov 22, 2026', trailing: Icons.calendar_today_outlined),
                const SizedBox(height: 10),
                _passengerField(),
                const SizedBox(height: 14),
                NLButton(label: 'Find Trips', icon: Icons.search, onPressed: () {}),
              ],
            ),
          ),
          const SizedBox(height: 20),
          const SectionLabel('Upcoming Trips'),
          for (final t in availableTrips) ...[
            _tripRow(t),
            const SizedBox(height: 10),
          ],
        ],
      ),
    );
  }

  Widget _field(String label, String value, {IconData? trailing}) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: NLColors.inputBg,
        borderRadius: BorderRadius.circular(NLRadii.button),
        border: Border.all(color: NLColors.border),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: NLText.muted.copyWith(fontSize: 11)),
                const SizedBox(height: 2),
                Text(value, style: NLText.body.copyWith(fontWeight: FontWeight.w600)),
              ],
            ),
          ),
          if (trailing != null) Icon(trailing, size: 18, color: NLColors.textMuted),
        ],
      ),
    );
  }

  Widget _passengerField() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: NLColors.inputBg,
        borderRadius: BorderRadius.circular(NLRadii.button),
        border: Border.all(color: NLColors.border),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Passengers', style: NLText.muted.copyWith(fontSize: 11)),
                const SizedBox(height: 2),
                Text('$_passengers Passenger${_passengers == 1 ? '' : 's'}',
                    style: NLText.body.copyWith(fontWeight: FontWeight.w600)),
              ],
            ),
          ),
          IconButton(
            onPressed:
                _passengers > 1 ? () => setState(() => _passengers--) : null,
            icon: const Icon(Icons.remove_circle_outline),
            color: NLColors.primary,
          ),
          IconButton(
            onPressed: () => setState(() => _passengers++),
            icon: const Icon(Icons.add_circle_outline),
            color: NLColors.primary,
          ),
        ],
      ),
    );
  }

  Widget _tripRow(AvailableTrip t) {
    return SectionCard(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      onTap: () {},
      child: Row(
        children: [
          SizedBox(
            width: 64,
            child: Text(t.time,
                style: NLText.body.copyWith(fontWeight: FontWeight.w700)),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                RouteText(
                    from: t.from,
                    to: t.to,
                    style: NLText.body.copyWith(fontWeight: FontWeight.w600)),
                const SizedBox(height: 2),
                Text('${t.seatsLeft} seats left', style: NLText.muted),
                Text('Earn up to ${t.maxPoints} pts',
                    style: NLText.muted.copyWith(
                        color: NLColors.goldTextDark, fontWeight: FontWeight.w600)),
              ],
            ),
          ),
          Text('\$${t.price}',
              style: NLText.body.copyWith(fontWeight: FontWeight.w700, fontSize: 15)),
        ],
      ),
    );
  }
}

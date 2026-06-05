import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../trips/domain/entities/trip.dart';
import '../../../trips/domain/entities/trip_passenger.dart';
import '../../../trips/presentation/providers/trips_provider.dart';
import '../../../trips/presentation/widgets/add_passenger_sheet.dart';

class BookSeatPage extends ConsumerWidget {
  final Trip trip;
  const BookSeatPage({super.key, required this.trip});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final booked = trip.passengers
        .where((p) => p.paymentStatus != PassengerPaymentStatus.cancelled)
        .length;
    final capacity = trip.seatCapacity ?? 0;
    final remaining = capacity - booked;

    void onRefresh() {
      ref.invalidate(tripsProvider);
      if (context.mounted) Navigator.of(context).pop();
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(1),
          child: Container(height: 1, color: const Color(0xFFE5E7EB)),
        ),
        leading: IconButton(
          onPressed: () => Navigator.of(context).pop(),
          icon: const Icon(Icons.arrow_back_rounded),
          color: const Color(0xFF111827),
        ),
        title: const Text(
          'Book a Seat',
          style: TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w700,
            color: Color(0xFF111827),
          ),
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _TripSummaryCard(
              trip: trip,
              booked: booked,
              capacity: capacity,
              remaining: remaining,
            ),
            const SizedBox(height: 24),
            const Text(
              'Passenger Details',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w700,
                color: Color(0xFF111827),
              ),
            ),
            const SizedBox(height: 8),
            Text(
              remaining > 0
                  ? '$remaining seat${remaining == 1 ? '' : 's'} available on this run.'
                  : 'This run is at capacity.',
              style: const TextStyle(
                fontSize: 13,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: 20),
            SizedBox(
              width: double.infinity,
              height: 52,
              child: FilledButton.icon(
                onPressed: remaining > 0
                    ? () => showAddPassengerSheet(
                          context,
                          tripId: trip.id,
                          onRefresh: onRefresh,
                        )
                    : null,
                icon: const Icon(Icons.person_add_alt_1_rounded, size: 20),
                label: const Text('Add Passenger'),
                style: FilledButton.styleFrom(
                  backgroundColor: const Color(0xFF0F766E),
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  elevation: 0,
                  textStyle: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TripSummaryCard extends StatelessWidget {
  final Trip trip;
  final int booked;
  final int capacity;
  final int remaining;

  const _TripSummaryCard({
    required this.trip,
    required this.booked,
    required this.capacity,
    required this.remaining,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFF0F766E).withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFF0F766E).withValues(alpha: 0.2)),
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 32,
                height: 32,
                decoration: BoxDecoration(
                  color: const Color(0xFF0F766E).withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: const Icon(Icons.route_rounded,
                    size: 16, color: Color(0xFF0F766E)),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  '${trip.firstStopLocation ?? 'Start'} → ${trip.lastStopLocation ?? 'End'}',
                  style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 16,
            runSpacing: 8,
            children: [
              _SummaryItem(
                icon: Icons.schedule_rounded,
                label: _formatDateTime(trip.scheduledAt),
              ),
              _SummaryItem(
                icon: Icons.event_seat_rounded,
                label: '$remaining of $capacity seats left',
                color: remaining < 5
                    ? const Color(0xFFD97706)
                    : const Color(0xFF0F766E),
              ),
              if (trip.pricePerSeat != null)
                _SummaryItem(
                  icon: Icons.payments_outlined,
                  label: 'TTD ${trip.pricePerSeat!.toStringAsFixed(2)} / seat',
                  color: const Color(0xFF7C3AED),
                ),
            ],
          ),
        ],
      ),
    );
  }

  String _formatDateTime(DateTime dt) {
    final d = dt.toLocal();
    final months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
    ];
    final h = d.hour % 12 == 0 ? 12 : d.hour % 12;
    final m = d.minute.toString().padLeft(2, '0');
    final ampm = d.hour < 12 ? 'AM' : 'PM';
    return '${months[d.month - 1]} ${d.day}, $h:$m $ampm';
  }
}

class _SummaryItem extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color color;

  const _SummaryItem({
    required this.icon,
    required this.label,
    this.color = AppColors.brandGray,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 14, color: color),
        const SizedBox(width: 4),
        Text(
          label,
          style: TextStyle(
            fontSize: 13,
            color: color,
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }
}

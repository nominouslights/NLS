import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../trips/domain/entities/trip.dart';
import '../../../trips/domain/entities/trip_passenger.dart';
import '../../../trips/presentation/providers/trips_provider.dart';
import 'book_seat_page.dart';

class AvailableRunsPage extends ConsumerWidget {
  const AvailableRunsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripsAsync = ref.watch(tripsProvider);

    return tripsAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline_rounded,
                size: 40, color: AppColors.danger),
            const SizedBox(height: 8),
            Text('$e',
                style: const TextStyle(color: AppColors.danger),
                textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: () => ref.invalidate(tripsProvider),
              child: const Text('Retry'),
            ),
          ],
        ),
      ),
      data: (trips) {
        final now = DateTime.now();
        final available = trips
            .where((t) =>
                t.serviceType == TripServiceType.community &&
                (t.status == TripStatus.scheduled ||
                    t.status == TripStatus.dispatched) &&
                t.scheduledAt.isAfter(now) &&
                (t.seatCapacity ?? 0) >
                    t.passengers
                        .where((p) =>
                            p.paymentStatus != PassengerPaymentStatus.cancelled)
                        .length)
            .toList();

        if (available.isEmpty) {
          return const Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(Icons.directions_bus_rounded,
                    size: 64, color: Color(0xFFE5E7EB)),
                SizedBox(height: 16),
                Text(
                  'No available runs at this time',
                  style: TextStyle(
                    fontSize: 16,
                    color: AppColors.brandGray,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
            ),
          );
        }

        return RefreshIndicator(
          onRefresh: () => ref.refresh(tripsProvider.future),
          child: ListView.builder(
            padding: const EdgeInsets.all(16),
            itemCount: available.length,
            itemBuilder: (_, i) => _RunCard(
              trip: available[i],
              onBook: () => Navigator.of(context).push(
                MaterialPageRoute(
                  builder: (_) => BookSeatPage(trip: available[i]),
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}

class _RunCard extends StatelessWidget {
  final Trip trip;
  final VoidCallback onBook;

  const _RunCard({required this.trip, required this.onBook});

  @override
  Widget build(BuildContext context) {
    final booked = trip.passengers
        .where((p) => p.paymentStatus != PassengerPaymentStatus.cancelled)
        .length;
    final capacity = trip.seatCapacity ?? 0;
    final remaining = capacity - booked;

    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE5E7EB)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.04),
            blurRadius: 12,
            offset: const Offset(0, 3),
          ),
        ],
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.route_rounded,
                    size: 18, color: AppColors.primary),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    '${trip.firstStopLocation ?? 'Start'} → ${trip.lastStopLocation ?? 'End'}',
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF111827),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                _InfoChip(
                  icon: Icons.schedule_rounded,
                  label: _formatDateTime(trip.scheduledAt),
                ),
                const SizedBox(width: 8),
                _InfoChip(
                  icon: Icons.event_seat_rounded,
                  label: '$remaining / $capacity seats',
                  color: remaining < 5
                      ? const Color(0xFFD97706)
                      : const Color(0xFF0F766E),
                ),
                if (trip.pricePerSeat != null) ...[
                  const SizedBox(width: 8),
                  _InfoChip(
                    icon: Icons.payments_outlined,
                    label: 'TTD ${trip.pricePerSeat!.toStringAsFixed(2)}',
                    color: const Color(0xFF7C3AED),
                  ),
                ],
              ],
            ),
            const SizedBox(height: 14),
            SizedBox(
              width: double.infinity,
              height: 44,
              child: ElevatedButton.icon(
                onPressed: onBook,
                icon: const Icon(Icons.add_circle_outline_rounded, size: 18),
                label: const Text('Book Seat'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF0F766E),
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                  elevation: 0,
                ),
              ),
            ),
          ],
        ),
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

class _InfoChip extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color color;

  const _InfoChip({
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
          style: TextStyle(fontSize: 13, color: color, fontWeight: FontWeight.w500),
        ),
      ],
    );
  }
}

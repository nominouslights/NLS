import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../trips/domain/entities/trip.dart';
import '../../../trips/domain/entities/trip_passenger.dart';
import '../../../trips/presentation/providers/trips_provider.dart';

class CommunityDashboardPage extends ConsumerWidget {
  const CommunityDashboardPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripsAsync = ref.watch(tripsProvider);

    return tripsAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(child: Text('$e')),
      data: (trips) {
        final communityTrips = trips
            .where((t) => t.serviceType == TripServiceType.community)
            .toList();
        final now = DateTime.now();
        final today = DateTime(now.year, now.month, now.day);
        final todayTrips = communityTrips
            .where((t) =>
                t.scheduledAt.year == today.year &&
                t.scheduledAt.month == today.month &&
                t.scheduledAt.day == today.day)
            .toList();

        final seatsBookedToday = todayTrips.fold<int>(
          0,
          (sum, t) =>
              sum +
              t.passengers
                  .where((p) => p.paymentStatus != PassengerPaymentStatus.cancelled)
                  .length,
        );

        final upcomingTrips = communityTrips
            .where((t) =>
                t.status == TripStatus.scheduled ||
                t.status == TripStatus.dispatched)
            .toList();

        final totalCapacity = upcomingTrips.fold<int>(
          0,
          (sum, t) => sum + (t.seatCapacity ?? 0),
        );
        final totalBooked = upcomingTrips.fold<int>(
          0,
          (sum, t) =>
              sum +
              t.passengers
                  .where((p) => p.paymentStatus != PassengerPaymentStatus.cancelled)
                  .length,
        );
        final availableSeats = totalCapacity - totalBooked;

        final revenueCollected = communityTrips.fold<double>(
          0.0,
          (sum, t) =>
              sum +
              t.passengers
                  .where((p) => p.paymentStatus == PassengerPaymentStatus.paid)
                  .length *
                  (t.pricePerSeat ?? 0.0),
        );

        return SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Community Overview',
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                ),
              ),
              const SizedBox(height: 4),
              Text(
                'Real-time metrics for community per-seat runs.',
                style: TextStyle(fontSize: 14, color: AppColors.textSecondary),
              ),
              const SizedBox(height: 24),
              Wrap(
                spacing: 16,
                runSpacing: 16,
                children: [
                  _KpiCard(
                    label: "Today's Runs",
                    value: '${todayTrips.length}',
                    icon: Icons.today_rounded,
                    color: AppColors.primary,
                  ),
                  _KpiCard(
                    label: 'Seats Booked Today',
                    value: '$seatsBookedToday',
                    icon: Icons.event_seat_rounded,
                    color: const Color(0xFF7C3AED),
                  ),
                  _KpiCard(
                    label: 'Available Seats',
                    value: '$availableSeats',
                    icon: Icons.airline_seat_recline_normal_rounded,
                    color: const Color(0xFF0F766E),
                  ),
                  _KpiCard(
                    label: 'Revenue Collected',
                    value: 'TTD ${revenueCollected.toStringAsFixed(2)}',
                    icon: Icons.payments_outlined,
                    color: const Color(0xFFD97706),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );
  }
}

class _KpiCard extends StatelessWidget {
  final String label;
  final String value;
  final IconData icon;
  final Color color;

  const _KpiCard({
    required this.label,
    required this.value,
    required this.icon,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 200,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE5E7EB)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.05),
            blurRadius: 16,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(icon, color: color, size: 20),
          ),
          const SizedBox(height: 12),
          Text(
            value,
            style: TextStyle(
              fontSize: 28,
              fontWeight: FontWeight.w800,
              color: color,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            style: const TextStyle(
              fontSize: 13,
              color: Color(0xFF6B7280),
            ),
          ),
        ],
      ),
    );
  }
}

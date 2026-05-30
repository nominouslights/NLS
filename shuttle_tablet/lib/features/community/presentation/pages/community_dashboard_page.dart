import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../trips/domain/entities/trip.dart';
import '../../../trips/domain/entities/trip_passenger.dart';
import '../../../trips/presentation/providers/trips_provider.dart';

class CommunityDashboardPage extends ConsumerWidget {
  final VoidCallback? onCalendarTap;
  final VoidCallback? onBookingsTap;

  const CommunityDashboardPage({
    super.key,
    this.onCalendarTap,
    this.onBookingsTap,
  });

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
        final now = DateTime.now().toLocal();
        final today = DateTime(now.year, now.month, now.day);

        bool isActive(PassengerPaymentStatus s) =>
            s == PassengerPaymentStatus.tentative ||
            s == PassengerPaymentStatus.awaitingPayment ||
            s == PassengerPaymentStatus.confirmed;

        final todayTrips = communityTrips.where((t) {
          final d = t.scheduledAt.toLocal();
          return d.year == today.year &&
              d.month == today.month &&
              d.day == today.day;
        }).toList();

        final upcomingTrips = communityTrips
            .where((t) =>
                t.status == TripStatus.scheduled ||
                t.status == TripStatus.dispatched)
            .toList();

        final totalBooked = upcomingTrips.fold<int>(
          0,
          (sum, t) =>
              sum + t.passengers.where((p) => isActive(p.paymentStatus)).length,
        );

        final awaitingPayment = communityTrips.fold<int>(
          0,
          (sum, t) =>
              sum +
              t.passengers
                  .where((p) =>
                      p.paymentStatus == PassengerPaymentStatus.awaitingPayment)
                  .length,
        );

        final totalCapacity = upcomingTrips.fold<int>(
          0,
          (sum, t) => sum + (t.seatCapacity ?? 0),
        );
        final availableSeats = totalCapacity - totalBooked;

        final revenueCollected = communityTrips.fold<double>(
          0.0,
          (sum, t) =>
              sum +
              t.passengers
                  .where(
                      (p) => p.paymentStatus == PassengerPaymentStatus.confirmed)
                  .fold<double>(0.0, (s, p) => s + (p.fare ?? 90.0)),
        );

        final todayPassengers = [
          for (final t in todayTrips)
            for (final p in t.passengers)
              if (isActive(p.paymentStatus)) (trip: t, passenger: p),
        ];

        return RefreshIndicator(
          onRefresh: () => ref.read(tripsProvider.notifier).refresh(),
          child: SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
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
                  style:
                      TextStyle(fontSize: 14, color: AppColors.textSecondary),
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
                      label: 'Seats Booked',
                      value: '$totalBooked',
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
                      value: '\$${revenueCollected.toStringAsFixed(0)}',
                      icon: Icons.payments_outlined,
                      color: const Color(0xFFD97706),
                    ),
                    _KpiCard(
                      label: 'Awaiting Payment',
                      value: '$awaitingPayment',
                      icon: Icons.schedule_rounded,
                      color: const Color(0xFFEA580C),
                    ),
                  ],
                ),
                const SizedBox(height: 20),
                Row(
                  children: [
                    Expanded(
                      child: _QuickActionButton(
                        icon: Icons.calendar_month_rounded,
                        label: 'Manage Calendar',
                        color: const Color(0xFF0F766E),
                        onTap: onCalendarTap,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _QuickActionButton(
                        icon: Icons.list_alt_rounded,
                        label: 'All Bookings',
                        color: const Color(0xFF7C3AED),
                        onTap: onBookingsTap,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 24),
                const Text(
                  "Today's Departures",
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827),
                  ),
                ),
                const SizedBox(height: 12),
                if (todayPassengers.isEmpty)
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.symmetric(
                        vertical: 20, horizontal: 16),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: const Color(0xFFE5E7EB)),
                    ),
                    child: const Text(
                      'No departures today.',
                      textAlign: TextAlign.center,
                      style:
                          TextStyle(fontSize: 13, color: Color(0xFF9CA3AF)),
                    ),
                  )
                else
                  ...todayPassengers.map(
                    (r) => Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: _TodayPassengerRow(
                        passenger: r.passenger,
                      ),
                    ),
                  ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _QuickActionButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color color;
  final VoidCallback? onTap;

  const _QuickActionButton({
    required this.icon,
    required this.label,
    required this.color,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding:
            const EdgeInsets.symmetric(vertical: 14, horizontal: 16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: const Color(0xFFE5E7EB)),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.04),
              blurRadius: 8,
              offset: const Offset(0, 2),
            ),
          ],
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, color: color, size: 18),
            const SizedBox(width: 8),
            Text(
              label,
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: color,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TodayPassengerRow extends StatelessWidget {
  final TripPassenger passenger;

  const _TodayPassengerRow({required this.passenger});

  @override
  Widget build(BuildContext context) {
    final (statusLabel, statusColor) = _statusStyle(passenger.paymentStatus);

    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  passenger.name,
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: Color(0xFF111827),
                  ),
                ),
                if (passenger.direction != null)
                  Text(
                    passenger.direction!,
                    style: const TextStyle(
                        fontSize: 12, color: Color(0xFF6B7280)),
                  ),
              ],
            ),
          ),
          Container(
            padding:
                const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(6),
              border:
                  Border.all(color: statusColor.withValues(alpha: 0.3)),
            ),
            child: Text(
              statusLabel,
              style: TextStyle(
                fontSize: 10,
                fontWeight: FontWeight.w700,
                color: statusColor,
              ),
            ),
          ),
        ],
      ),
    );
  }

  (String, Color) _statusStyle(PassengerPaymentStatus status) {
    return switch (status) {
      PassengerPaymentStatus.confirmed || PassengerPaymentStatus.paid =>
        ('Confirmed', const Color(0xFF059669)),
      PassengerPaymentStatus.awaitingPayment =>
        ('Awaiting', const Color(0xFFEA580C)),
      _ => ('Tentative', const Color(0xFFD97706)),
    };
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

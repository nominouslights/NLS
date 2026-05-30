import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../trips/domain/entities/trip.dart';
import '../../../trips/domain/entities/trip_passenger.dart';
import '../../../trips/domain/repositories/i_trip_repository.dart';
import '../../../trips/domain/usecases/update_passenger_payment_status_usecase.dart';
import '../../../trips/presentation/providers/trips_provider.dart';
import '../providers/calendar_provider.dart';

class AdminBookingsListPage extends ConsumerStatefulWidget {
  const AdminBookingsListPage({super.key});

  @override
  ConsumerState<AdminBookingsListPage> createState() =>
      _AdminBookingsListPageState();
}

class _AdminBookingsListPageState
    extends ConsumerState<AdminBookingsListPage> {
  PassengerPaymentStatus? _statusFilter;
  String? _directionFilter;

  @override
  Widget build(BuildContext context) {
    final tripsAsync = ref.watch(tripsProvider);

    return Column(
      children: [
        Container(
          color: Colors.white,
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
          child: Column(
            children: [
              // Status filter chips
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: [
                    _FilterChip(
                      label: 'All',
                      selected: _statusFilter == null,
                      onTap: () => setState(() => _statusFilter = null),
                    ),
                    const SizedBox(width: 6),
                    _FilterChip(
                      label: 'Tentative',
                      selected:
                          _statusFilter == PassengerPaymentStatus.tentative,
                      color: const Color(0xFFD97706),
                      onTap: () => setState(
                          () => _statusFilter = PassengerPaymentStatus.tentative),
                    ),
                    const SizedBox(width: 6),
                    _FilterChip(
                      label: 'Awaiting',
                      selected: _statusFilter ==
                          PassengerPaymentStatus.awaitingPayment,
                      color: const Color(0xFFEA580C),
                      onTap: () => setState(() =>
                          _statusFilter = PassengerPaymentStatus.awaitingPayment),
                    ),
                    const SizedBox(width: 6),
                    _FilterChip(
                      label: 'Confirmed',
                      selected:
                          _statusFilter == PassengerPaymentStatus.confirmed,
                      color: const Color(0xFF059669),
                      onTap: () => setState(() =>
                          _statusFilter = PassengerPaymentStatus.confirmed),
                    ),
                    const SizedBox(width: 6),
                    _FilterChip(
                      label: 'Released',
                      selected:
                          _statusFilter == PassengerPaymentStatus.released,
                      color: AppColors.danger,
                      onTap: () => setState(
                          () => _statusFilter = PassengerPaymentStatus.released),
                    ),
                    const SizedBox(width: 6),
                    _FilterChip(
                      label: 'Cancelled',
                      selected:
                          _statusFilter == PassengerPaymentStatus.cancelled,
                      color: AppColors.brandGray,
                      onTap: () => setState(() =>
                          _statusFilter = PassengerPaymentStatus.cancelled),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 8),
              // Direction filter
              Row(
                children: [
                  _ToggleButton(
                    label: 'All',
                    selected: _directionFilter == null,
                    onTap: () => setState(() => _directionFilter = null),
                  ),
                  const SizedBox(width: 6),
                  _ToggleButton(
                    label: 'Outbound →',
                    selected: _directionFilter == 'Outbound',
                    onTap: () =>
                        setState(() => _directionFilter = 'Outbound'),
                  ),
                  const SizedBox(width: 6),
                  _ToggleButton(
                    label: '← Inbound',
                    selected: _directionFilter == 'Inbound',
                    onTap: () =>
                        setState(() => _directionFilter = 'Inbound'),
                  ),
                ],
              ),
            ],
          ),
        ),
        const Divider(height: 1, color: Color(0xFFE5E7EB)),
        Expanded(
          child: tripsAsync.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (e, _) => Center(child: Text('$e')),
            data: (trips) {
              final communityTrips = trips
                  .where((t) => t.serviceType == TripServiceType.community)
                  .toList();

              final rows = communityTrips
                  .expand((t) => t.passengers.map((p) => _PassengerRow(
                        trip: t,
                        passenger: p,
                      )))
                  .where((row) {
                    if (_statusFilter != null &&
                        row.passenger.paymentStatus != _statusFilter) {
                      return false;
                    }
                    if (_directionFilter != null &&
                        row.passenger.direction != _directionFilter) {
                      return false;
                    }
                    return true;
                  })
                  .toList()
                ..sort((a, b) =>
                    a.trip.scheduledAt.compareTo(b.trip.scheduledAt));

              if (rows.isEmpty) {
                return const Center(
                  child: Text('No bookings match this filter.',
                      style: TextStyle(
                          color: Color(0xFF9CA3AF), fontSize: 14)),
                );
              }

              return RefreshIndicator(
                onRefresh: () => ref.read(tripsProvider.notifier).refresh(),
                child: ListView.separated(
                  padding: const EdgeInsets.all(12),
                  itemCount: rows.length,
                  separatorBuilder: (_, __) =>
                      const SizedBox(height: 8),
                  itemBuilder: (context, i) =>
                      _BookingRowCard(row: rows[i], ref: ref),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}

class _PassengerRow {
  final Trip trip;
  final TripPassenger passenger;
  const _PassengerRow({required this.trip, required this.passenger});
}

class _BookingRowCard extends StatelessWidget {
  final _PassengerRow row;
  final WidgetRef ref;
  const _BookingRowCard({required this.row, required this.ref});

  @override
  Widget build(BuildContext context) {
    final p = row.passenger;
    final trip = row.trip;
    final months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
    ];
    final dep = trip.scheduledAt.toLocal();
    final depStr =
        '${months[dep.month - 1]} ${dep.day} — ${p.direction ?? ''}';

    final canPay = p.paymentStatus == PassengerPaymentStatus.tentative ||
        p.paymentStatus == PassengerPaymentStatus.awaitingPayment;

    final (statusLabel, statusColor) = _statusStyle(p.paymentStatus);

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      padding: const EdgeInsets.all(12),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(p.name,
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF111827),
                    )),
                const SizedBox(height: 3),
                Text(
                    '${p.bookingReference ?? '—'} · $depStr',
                    style: const TextStyle(
                        fontSize: 12, color: Color(0xFF6B7280))),
                if (p.cutoffDeadline != null &&
                    (p.paymentStatus == PassengerPaymentStatus.tentative ||
                        p.paymentStatus ==
                            PassengerPaymentStatus.awaitingPayment)) ...[
                  const SizedBox(height: 2),
                  Text(
                    'Deadline: Thu ${months[p.cutoffDeadline!.toLocal().month - 1]} ${p.cutoffDeadline!.toLocal().day} at 6 PM CT',
                    style: const TextStyle(
                        fontSize: 11, color: Color(0xFFEA580C)),
                  ),
                ],
              ],
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: statusColor.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(6),
                  border: Border.all(
                      color: statusColor.withValues(alpha: 0.3)),
                ),
                child: Text(statusLabel,
                    style: TextStyle(
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                      color: statusColor,
                    )),
              ),
              if (canPay) ...[
                const SizedBox(height: 6),
                TextButton(
                  onPressed: () => _markPaid(context, trip.id, p.id),
                  style: TextButton.styleFrom(
                    foregroundColor: const Color(0xFF0F766E),
                    padding: const EdgeInsets.symmetric(
                        horizontal: 8, vertical: 2),
                    minimumSize: Size.zero,
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  ),
                  child: const Text('Mark Paid',
                      style: TextStyle(fontSize: 12)),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }

  Future<void> _markPaid(
      BuildContext context, String tripId, String passengerId) async {
    final result = await sl<UpdatePassengerPaymentStatusUseCase>()(
      UpdatePassengerPaymentStatusParams(
        tripId: tripId,
        passengerId: passengerId,
        paymentStatus: PassengerPaymentStatus.confirmed,
      ),
    );
    result.fold(
      (f) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text(f.message),
                backgroundColor: AppColors.danger),
          );
        }
      },
      (_) {
        ref.invalidate(tripsProvider);
        ref.invalidate(adminCalendarProvider);
      },
    );
  }

  (String, Color) _statusStyle(PassengerPaymentStatus status) {
    return switch (status) {
      PassengerPaymentStatus.confirmed || PassengerPaymentStatus.paid =>
        ('Confirmed', const Color(0xFF059669)),
      PassengerPaymentStatus.awaitingPayment =>
        ('Awaiting', const Color(0xFFEA580C)),
      PassengerPaymentStatus.released => ('Released', AppColors.danger),
      PassengerPaymentStatus.cancelled =>
        ('Cancelled', AppColors.brandGray),
      _ => ('Tentative', const Color(0xFFD97706)),
    };
  }
}

class _FilterChip extends StatelessWidget {
  final String label;
  final bool selected;
  final Color color;
  final VoidCallback onTap;

  const _FilterChip({
    required this.label,
    required this.selected,
    this.color = const Color(0xFF0F766E),
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: selected ? color.withValues(alpha: 0.12) : Colors.white,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(
            color: selected ? color : const Color(0xFFE5E7EB),
          ),
        ),
        child: Text(label,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: selected ? color : const Color(0xFF6B7280),
            )),
      ),
    );
  }
}

class _ToggleButton extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _ToggleButton({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
        decoration: BoxDecoration(
          color: selected
              ? const Color(0xFF0F766E).withValues(alpha: 0.1)
              : Colors.white,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(
            color: selected
                ? const Color(0xFF0F766E)
                : const Color(0xFFE5E7EB),
          ),
        ),
        child: Text(label,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: selected
                  ? const Color(0xFF0F766E)
                  : const Color(0xFF6B7280),
            )),
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../trips/domain/entities/trip_passenger.dart';
import '../../../trips/domain/repositories/i_trip_repository.dart';
import '../../../trips/domain/usecases/update_passenger_payment_status_usecase.dart';
import '../../../trips/presentation/providers/trips_provider.dart';
import '../../domain/entities/calendar_day.dart';
import '../../domain/repositories/i_community_repository.dart';
import '../../domain/usecases/block_day_usecase.dart';
import '../../domain/usecases/unblock_day_usecase.dart';
import '../providers/calendar_provider.dart';

void showDayDetailSheet(
    BuildContext context, WidgetRef ref, CalendarDay day) {
  showModalBottomSheet(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => UncontrolledProviderScope(
      container: ProviderScope.containerOf(context),
      child: _DayDetailSheet(day: day),
    ),
  );
}

class _DayDetailSheet extends ConsumerStatefulWidget {
  final CalendarDay day;
  const _DayDetailSheet({required this.day});

  @override
  ConsumerState<_DayDetailSheet> createState() => _DayDetailSheetState();
}

class _DayDetailSheetState extends ConsumerState<_DayDetailSheet> {
  final _reasonController = TextEditingController();
  bool _blocking = false;
  bool _unblocking = false;

  @override
  void dispose() {
    _reasonController.dispose();
    super.dispose();
  }

  Future<void> _markPaid(String tripId, String passengerId) async {
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
            SnackBar(content: Text(f.message), backgroundColor: AppColors.danger),
          );
        }
      },
      (_) {
        ref.invalidate(adminCalendarProvider);
        ref.invalidate(tripsProvider);
        if (context.mounted) Navigator.of(context).pop();
      },
    );
  }

  Future<void> _blockDay(String reason) async {
    setState(() => _blocking = true);
    final dateStr =
        '${widget.day.date.year}-${widget.day.date.month.toString().padLeft(2, '0')}-${widget.day.date.day.toString().padLeft(2, '0')}';
    final result = await sl<BlockDayUseCase>()(
      BlockDayParams(date: dateStr, reason: reason),
    );
    if (!mounted) return;
    setState(() => _blocking = false);
    result.fold(
      (f) => ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(f.message), backgroundColor: AppColors.danger),
      ),
      (cancelled) {
        ref.invalidate(adminCalendarProvider);
        Navigator.of(context).pop();
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Day blocked. $cancelled passenger(s) notified.')),
        );
      },
    );
  }

  Future<void> _unblockDay() async {
    setState(() => _unblocking = true);
    final dateStr =
        '${widget.day.date.year}-${widget.day.date.month.toString().padLeft(2, '0')}-${widget.day.date.day.toString().padLeft(2, '0')}';
    final result = await sl<UnblockDayUseCase>()(dateStr);
    if (!mounted) return;
    setState(() => _unblocking = false);
    result.fold(
      (f) => ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(f.message), backgroundColor: AppColors.danger),
      ),
      (_) {
        ref.invalidate(adminCalendarProvider);
        Navigator.of(context).pop();
      },
    );
  }

  void _confirmBlock() {
    _reasonController.clear();
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Block This Day'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
                'All active passengers on this day will be notified and their seats cancelled. This cannot be undone.'),
            const SizedBox(height: 14),
            TextField(
              controller: _reasonController,
              decoration: const InputDecoration(
                labelText: 'Reason (internal)',
                hintText: 'e.g. Road conditions, scheduling conflict',
                border: OutlineInputBorder(),
              ),
              maxLines: 2,
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: const Text('Cancel'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.danger,
              foregroundColor: Colors.white,
            ),
            onPressed: () {
              final reason = _reasonController.text.trim();
              if (reason.isEmpty) return;
              Navigator.of(ctx).pop();
              _blockDay(reason);
            },
            child: const Text('Confirm Block'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final day = widget.day;
    final screenHeight = MediaQuery.sizeOf(context).height;
    final months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
    ];
    final dateStr =
        '${day.dayOfWeek}, ${months[day.date.month - 1]} ${day.date.day}';

    return Container(
      constraints: BoxConstraints(maxHeight: screenHeight * 0.80),
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(height: 12),
          Container(
            width: 40,
            height: 4,
            decoration: BoxDecoration(
              color: const Color(0xFFE5E7EB),
              borderRadius: BorderRadius.circular(2),
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 16, 20, 0),
            child: Row(
              children: [
                Expanded(
                  child: Text(dateStr,
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF111827),
                      )),
                ),
                if (day.isBlocked) ...[
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.danger.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: const Text('BLOCKED',
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                          color: AppColors.danger,
                        )),
                  ),
                ] else ...[
                  _StatusChip(day: day),
                ],
              ],
            ),
          ),
          const SizedBox(height: 12),
          if (day.isBlocked)
            _BlockedContent(
              day: day,
              onUnblock: _unblockDay,
              unblocking: _unblocking,
            )
          else
            _AvailableContent(
              day: day,
              onMarkPaid: _markPaid,
              onBlock: _confirmBlock,
              blocking: _blocking,
            ),
        ],
      ),
    );
  }
}

class _AvailableContent extends ConsumerWidget {
  final CalendarDay day;
  final Future<void> Function(String tripId, String passengerId) onMarkPaid;
  final VoidCallback onBlock;
  final bool blocking;

  const _AvailableContent({
    required this.day,
    required this.onMarkPaid,
    required this.onBlock,
    required this.blocking,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripsAsync = ref.watch(tripsProvider);

    return Expanded(
      child: Column(
        children: [
          // Threshold bar
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Text(
                        '${day.confirmedCount} of 2 minimum',
                        style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF374151),
                        )),
                    const Spacer(),
                    if (day.confirmedCount < 2)
                      Text(
                          '${2 - day.confirmedCount} more needed to GO',
                          style: const TextStyle(
                            fontSize: 12,
                            color: Color(0xFFEA580C),
                          )),
                    if (day.confirmedCount >= 2)
                      const Text('Minimum met — GO',
                          style: TextStyle(
                            fontSize: 12,
                            color: Color(0xFF15803D),
                            fontWeight: FontWeight.w600,
                          )),
                  ],
                ),
                const SizedBox(height: 6),
                LinearProgressIndicator(
                  value: (day.confirmedCount / 2).clamp(0.0, 1.0),
                  minHeight: 6,
                  backgroundColor: const Color(0xFFE5E7EB),
                  valueColor: AlwaysStoppedAnimation<Color>(
                    day.confirmedCount >= 2
                        ? const Color(0xFF15803D)
                        : const Color(0xFFEA580C),
                  ),
                  borderRadius: BorderRadius.circular(3),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          const Divider(height: 1, color: Color(0xFFE5E7EB)),
          Expanded(
            child: tripsAsync.when(
              loading: () =>
                  const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(child: Text('$e')),
              data: (trips) {
                final dayDate = day.date;
                final dayTrips = trips.where((t) =>
                    t.scheduledAt.year == dayDate.year &&
                    t.scheduledAt.month == dayDate.month &&
                    t.scheduledAt.day == dayDate.day).toList();

                final passengers = dayTrips
                    .expand((t) =>
                        t.passengers.map((p) => (trip: t, passenger: p)))
                    .where((tp) =>
                        tp.passenger.paymentStatus !=
                            PassengerPaymentStatus.cancelled &&
                        tp.passenger.paymentStatus !=
                            PassengerPaymentStatus.released)
                    .toList();

                if (passengers.isEmpty) {
                  return const Center(
                    child: Text('No bookings yet for this day.',
                        style: TextStyle(
                            color: Color(0xFF9CA3AF), fontSize: 14)),
                  );
                }

                return ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: passengers.length,
                  separatorBuilder: (_, __) => const Divider(height: 1),
                  itemBuilder: (context, i) {
                    final tp = passengers[i];
                    final p = tp.passenger;
                    final tripId = tp.trip.id;
                    final canPay = p.paymentStatus ==
                            PassengerPaymentStatus.tentative ||
                        p.paymentStatus ==
                            PassengerPaymentStatus.awaitingPayment;

                    return ListTile(
                      contentPadding:
                          const EdgeInsets.symmetric(horizontal: 4),
                      title: Text(p.name,
                          style: const TextStyle(
                              fontWeight: FontWeight.w600, fontSize: 14)),
                      subtitle: Text(
                          '${p.direction ?? ''} · ${p.bookingReference ?? ''}',
                          style: const TextStyle(
                              fontSize: 12,
                              color: Color(0xFF6B7280))),
                      trailing: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          _StatusPill(p.paymentStatus),
                          if (canPay) ...[
                            const SizedBox(width: 8),
                            TextButton(
                              onPressed: () => onMarkPaid(tripId, p.id),
                              style: TextButton.styleFrom(
                                foregroundColor: const Color(0xFF0F766E),
                                padding: const EdgeInsets.symmetric(
                                    horizontal: 8, vertical: 4),
                                minimumSize: Size.zero,
                                tapTargetSize:
                                    MaterialTapTargetSize.shrinkWrap,
                              ),
                              child: const Text('Mark Paid',
                                  style: TextStyle(fontSize: 12)),
                            ),
                          ],
                        ],
                      ),
                    );
                  },
                );
              },
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 20),
            child: SizedBox(
              width: double.infinity,
              height: 44,
              child: OutlinedButton.icon(
                onPressed: blocking ? null : onBlock,
                icon: blocking
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2))
                    : const Icon(Icons.block_rounded, size: 18),
                label: const Text('Block This Day'),
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColors.danger,
                  side: const BorderSide(color: AppColors.danger),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10)),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _BlockedContent extends StatelessWidget {
  final CalendarDay day;
  final VoidCallback onUnblock;
  final bool unblocking;

  const _BlockedContent({
    required this.day,
    required this.onUnblock,
    required this.unblocking,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (day.blockReason != null) ...[
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.danger.withValues(alpha: 0.05),
                borderRadius: BorderRadius.circular(10),
                border: Border.all(
                    color: AppColors.danger.withValues(alpha: 0.2)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Reason',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: AppColors.danger,
                      )),
                  const SizedBox(height: 4),
                  Text(day.blockReason!,
                      style: const TextStyle(
                        fontSize: 13,
                        color: Color(0xFF374151),
                      )),
                ],
              ),
            ),
            const SizedBox(height: 16),
          ],
          SizedBox(
            width: double.infinity,
            height: 44,
            child: ElevatedButton(
              onPressed: unblocking ? null : onUnblock,
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF0F766E),
                foregroundColor: Colors.white,
                elevation: 0,
                shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10)),
              ),
              child: unblocking
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(
                          strokeWidth: 2, color: Colors.white))
                  : const Text('Unblock This Day'),
            ),
          ),
        ],
      ),
    );
  }
}

class _StatusChip extends StatelessWidget {
  final CalendarDay day;
  const _StatusChip({required this.day});

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (day.status) {
      CalendarDayStatus.go => ('GO', const Color(0xFF15803D)),
      CalendarDayStatus.building => ('BUILDING', const Color(0xFFEA580C)),
      CalendarDayStatus.open => ('OPEN', const Color(0xFF6B7280)),
      CalendarDayStatus.unavailable => ('UNAVAILABLE', AppColors.danger),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(label,
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w700,
            color: color,
          )),
    );
  }
}

class _StatusPill extends StatelessWidget {
  final PassengerPaymentStatus status;
  const _StatusPill(this.status);

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      PassengerPaymentStatus.confirmed ||
      PassengerPaymentStatus.paid =>
        ('✓ Paid', const Color(0xFF059669)),
      PassengerPaymentStatus.awaitingPayment =>
        ('Due', const Color(0xFFEA580C)),
      _ => ('Hold', const Color(0xFFD97706)),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Text(label,
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w600,
            color: color,
          )),
    );
  }
}

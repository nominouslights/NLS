import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip.dart';
import '../../domain/entities/trip_passenger.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../../domain/usecases/add_passenger_usecase.dart';
import '../../domain/usecases/remove_passenger_usecase.dart';
import '../../domain/usecases/update_passenger_payment_status_usecase.dart';
import '../../../drivers/domain/entities/driver.dart';
import '../../../drivers/presentation/providers/drivers_provider.dart';
import '../providers/trip_form_provider.dart';
import '../providers/trips_provider.dart';
import 'trip_status_badge.dart';
import 'trip_stop_list.dart';

class TripDetailWorkspace extends ConsumerWidget {
  final String tripId;
  final VoidCallback? onDeleted;
  const TripDetailWorkspace({super.key, required this.tripId, this.onDeleted});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripAsync = ref.watch(tripDetailProvider(tripId));

    return tripAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline_rounded,
                size: 48, color: AppColors.danger),
            const SizedBox(height: 12),
            Text('$e', style: const TextStyle(color: AppColors.danger)),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: () => ref.invalidate(tripDetailProvider(tripId)),
              child: const Text('Retry'),
            ),
          ],
        ),
      ),
      data: (trip) => _TripDetail(trip: trip, onDeleted: onDeleted, onRefresh: () {
        ref.invalidate(tripDetailProvider(tripId));
        ref.invalidate(tripsProvider);
      }),
    );
  }
}

class _TripDetail extends ConsumerWidget {
  final Trip trip;
  final VoidCallback onRefresh;
  final VoidCallback? onDeleted;

  const _TripDetail({required this.trip, required this.onRefresh, this.onDeleted});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final drivers = ref.watch(driversProvider).valueOrNull ?? [];
    final driverName = trip.driverId == null
        ? 'Unassigned'
        : drivers
                .cast<Driver?>()
                .firstWhere((d) => d?.id == trip.driverId, orElse: () => null)
                ?.fullName ??
            'Unassigned';

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      trip.purchaseOrderNumber != null
                          ? 'PO# ${trip.purchaseOrderNumber}'
                          : 'Trip',
                      style: const TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      DateFormat('EEEE, MMM d yyyy · h:mm a')
                          .format(trip.scheduledAt.toLocal()),
                      style: const TextStyle(
                        fontSize: 13,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              TripStatusBadge(trip.status),
              const SizedBox(width: 8),
              OutlinedButton.icon(
                onPressed: () => context.push('/driver/trips/${trip.id}'),
                icon: const Icon(Icons.open_in_new_rounded, size: 15),
                label: const Text('Full View'),
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColors.primary,
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  textStyle: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),
          const Divider(),
          const SizedBox(height: 16),

          // Route
          _SectionHeader('Route', Icons.route_rounded),
          const SizedBox(height: 12),
          TripStopList(trip.stops),
          const SizedBox(height: 20),
          const Divider(),
          const SizedBox(height: 16),

          // Trip details
          _SectionHeader('Details', Icons.info_outline_rounded),
          const SizedBox(height: 12),
          _DetailRow('Vehicle Type', trip.vehicleType ?? '—'),
          _DetailRow('Driver', driverName),
          _DetailRow('Created',
              DateFormat('MMM d, yyyy').format(trip.createdAt.toLocal())),
          if (trip.notes != null) _DetailRow('Notes', trip.notes!),
          const SizedBox(height: 20),
          const Divider(),
          const SizedBox(height: 16),

          // Status actions
          if (_showActions(trip.status)) ...[
            _SectionHeader('Actions', Icons.bolt_rounded),
            const SizedBox(height: 12),
            _ActionsBar(trip: trip, onRefresh: onRefresh, onDeleted: onDeleted),
            const SizedBox(height: 20),
            const Divider(),
            const SizedBox(height: 16),
          ],

          // Passenger manifest (community trips only)
          if (trip.serviceType == TripServiceType.community) ...[
            _PassengerManifest(trip: trip, onRefresh: onRefresh),
            const SizedBox(height: 20),
            const Divider(),
            const SizedBox(height: 16),
          ],

          // Pre-inspection summary
          if (trip.preInspection != null) ...[
            _SectionHeader('Pre-Trip Inspection', Icons.checklist_rounded),
            const SizedBox(height: 12),
            _InspectionSummary(inspection: trip.preInspection!),
            const SizedBox(height: 20),
            const Divider(),
            const SizedBox(height: 16),
          ],

          // Post-report summary
          if (trip.postReport != null) ...[
            _SectionHeader('Post-Trip Report', Icons.summarize_rounded),
            const SizedBox(height: 12),
            _PostReportSummary(report: trip.postReport!),
          ],
        ],
      ),
    );
  }

  bool _showActions(TripStatus s) => s != TripStatus.completed;
}

class _ActionsBar extends ConsumerWidget {
  final Trip trip;
  final VoidCallback onRefresh;
  final VoidCallback? onDeleted;
  const _ActionsBar({required this.trip, required this.onRefresh, this.onDeleted});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Wrap(
      spacing: 10,
      runSpacing: 8,
      children: [
        if (trip.status == TripStatus.scheduled && trip.driverId != null)
          FilledButton.icon(
            onPressed: () => _dispatch(context, ref),
            icon: const Icon(Icons.send_rounded, size: 16),
            label: const Text('Dispatch'),
            style: FilledButton.styleFrom(backgroundColor: AppColors.primary),
          ),
        if (trip.status == TripStatus.scheduled)
          OutlinedButton.icon(
            onPressed: () => _cancel(context, ref),
            icon: const Icon(Icons.cancel_outlined, size: 16),
            label: const Text('Cancel Trip'),
            style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger),
          ),
        OutlinedButton.icon(
          onPressed: () => _archive(context, ref),
          icon: const Icon(Icons.archive_outlined, size: 16),
          label: const Text('Archive Trip'),
          style: OutlinedButton.styleFrom(foregroundColor: AppColors.danger),
        ),
      ],
    );
  }

  Future<void> _dispatch(BuildContext context, WidgetRef ref) async {
    try {
      await ref.read(tripFormProvider.notifier).dispatchTrip(trip.id);
      onRefresh();
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Trip dispatched successfully')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed: $e'), backgroundColor: AppColors.danger),
        );
      }
    }
  }

  Future<void> _cancel(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Cancel Trip'),
        content: const Text('Are you sure you want to cancel this trip?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('No')),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
            child: const Text('Yes, Cancel'),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;
    try {
      await ref.read(tripFormProvider.notifier).assignDriver(
        trip.id,
        const AssignDriverParams(driverId: ''),
      );
    } catch (_) {}
    // Use status update to cancel
    await ref.read(tripsProvider.notifier).updateStatus(
          trip.id, TripStatus.cancelled);
    onRefresh();
  }

  Future<void> _archive(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Archive Trip'),
        content: const Text(
          'This trip will be moved to the archive. You can restore it later from the archive.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
            child: const Text('Archive'),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;
    try {
      await ref.read(tripsProvider.notifier).deleteTrip(trip.id);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Trip archived')),
        );
      }
      onDeleted?.call();
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Failed to archive: $e'),
            backgroundColor: AppColors.danger,
          ),
        );
      }
    }
  }
}

class _InspectionSummary extends StatelessWidget {
  final dynamic inspection; // TripPreInspection
  const _InspectionSummary({required this.inspection});

  @override
  Widget build(BuildContext context) {
    final failCount =
        (inspection.items as List).where((i) => !i.passed).length;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _DetailRow(
              'Odometer Start', '${inspection.odometerStart} km'),
          _DetailRow(
              'Submitted',
              DateFormat('MMM d, yyyy · h:mm a')
                  .format((inspection.submittedAt as DateTime).toLocal())),
          _DetailRow('Issues', failCount == 0 ? 'None' : '$failCount item(s) failed'),
        ],
      ),
    );
  }
}

class _PostReportSummary extends StatelessWidget {
  final dynamic report; // TripPostReport
  const _PostReportSummary({required this.report});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _DetailRow('Odometer Start', '${report.odometerStart} km'),
          _DetailRow('Odometer End', '${report.odometerEnd} km'),
          _DetailRow('Distance', '${report.distanceKm} km'),
          if (report.fuelAddedLitres != null)
            _DetailRow(
                'Fuel Added', '${report.fuelAddedLitres!.toStringAsFixed(1)} L'),
          if (report.fuelCostDollars != null)
            _DetailRow(
                'Fuel Cost', '\$${report.fuelCostDollars!.toStringAsFixed(2)}'),
          _DetailRow('Incident', report.hasIncident ? 'Yes' : 'No'),
          _DetailRow('Ready to Invoice', report.isReadyToInvoice ? 'Yes' : 'No'),
        ],
      ),
    );
  }
}

// ── Helpers ──────────────────────────────────────────────────────────────────

class _SectionHeader extends StatelessWidget {
  final String title;
  final IconData icon;
  const _SectionHeader(this.title, this.icon);

  @override
  Widget build(BuildContext context) => Row(
        children: [
          Icon(icon, size: 16, color: AppColors.primary),
          const SizedBox(width: 6),
          Text(
            title,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
              letterSpacing: 0.3,
            ),
          ),
        ],
      );
}

class _DetailRow extends StatelessWidget {
  final String label;
  final String value;
  const _DetailRow(this.label, this.value);

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 6),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 130,
              child: Text(
                label,
                style: const TextStyle(
                  fontSize: 13,
                  color: AppColors.textSecondary,
                ),
              ),
            ),
            Expanded(
              child: Text(
                value,
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textPrimary,
                ),
              ),
            ),
          ],
        ),
      );
}

// ── Passenger Manifest ───────────────────────────────────────────────────────

class _PassengerManifest extends StatelessWidget {
  final Trip trip;
  final VoidCallback onRefresh;

  const _PassengerManifest({required this.trip, required this.onRefresh});

  @override
  Widget build(BuildContext context) {
    final passengers = trip.passengers;
    final active = passengers
        .where((p) => p.paymentStatus != PassengerPaymentStatus.cancelled)
        .toList();
    final capacity = trip.seatCapacity ?? 0;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Icon(Icons.people_rounded, size: 16, color: AppColors.primary),
            const SizedBox(width: 6),
            Text(
              'Passengers (${active.length} / $capacity)',
              style: const TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w700,
                color: AppColors.textPrimary,
                letterSpacing: 0.3,
              ),
            ),
            const Spacer(),
            TextButton.icon(
              onPressed: () => _showAddPassengerDialog(context),
              icon: const Icon(Icons.person_add_alt_1_rounded, size: 16),
              label: const Text('Add'),
              style: TextButton.styleFrom(foregroundColor: const Color(0xFF0F766E)),
            ),
          ],
        ),
        const SizedBox(height: 10),
        if (passengers.isEmpty)
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: const Color(0xFFF9FAFB),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: const Color(0xFFE5E7EB)),
            ),
            child: const Center(
              child: Text(
                'No passengers yet',
                style: TextStyle(color: AppColors.brandGray, fontSize: 13),
              ),
            ),
          )
        else
          ...passengers.map(
            (p) => _PassengerCard(
              passenger: p,
              tripId: trip.id,
              onRefresh: onRefresh,
            ),
          ),
      ],
    );
  }

  Future<void> _showAddPassengerDialog(BuildContext context) async {
    await showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (ctx) => _AddPassengerSheet(tripId: trip.id, onRefresh: onRefresh),
    );
  }
}

class _PassengerCard extends StatelessWidget {
  final TripPassenger passenger;
  final String tripId;
  final VoidCallback onRefresh;

  const _PassengerCard({
    required this.passenger,
    required this.tripId,
    required this.onRefresh,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Row(
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: _statusColor(passenger.paymentStatus).withValues(alpha: 0.1),
              shape: BoxShape.circle,
            ),
            child: Center(
              child: Text(
                passenger.name.isNotEmpty
                    ? passenger.name[0].toUpperCase()
                    : '?',
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  color: _statusColor(passenger.paymentStatus),
                ),
              ),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  passenger.name,
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 14,
                    color: Color(0xFF111827),
                  ),
                ),
                if (passenger.contactInfo != null)
                  Text(
                    passenger.contactInfo!,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
              ],
            ),
          ),
          if (passenger.seatNumber != null)
            Container(
              margin: const EdgeInsets.only(right: 8),
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
              decoration: BoxDecoration(
                color: const Color(0xFFF3F4F6),
                borderRadius: BorderRadius.circular(6),
              ),
              child: Text(
                'Seat ${passenger.seatNumber}',
                style: const TextStyle(
                    fontSize: 11, fontWeight: FontWeight.w600),
              ),
            ),
          _StatusBadge(passenger.paymentStatus),
          const SizedBox(width: 4),
          PopupMenuButton<String>(
            icon: const Icon(Icons.more_vert_rounded,
                size: 18, color: AppColors.brandGray),
            onSelected: (v) => _handleAction(context, v),
            itemBuilder: (_) => [
              const PopupMenuItem(
                  value: 'pending',
                  child: Text('Mark Pending')),
              const PopupMenuItem(
                  value: 'paid',
                  child: Text('Mark Paid')),
              const PopupMenuItem(
                  value: 'cancel',
                  child: Text('Cancel Booking')),
              const PopupMenuDivider(),
              const PopupMenuItem(
                  value: 'remove',
                  child: Text('Remove Passenger',
                      style: TextStyle(color: AppColors.danger))),
            ],
          ),
        ],
      ),
    );
  }

  Future<void> _handleAction(BuildContext context, String action) async {
    if (action == 'remove') {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Remove Passenger'),
          content: Text('Remove ${passenger.name} from this trip?'),
          actions: [
            TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: const Text('Cancel')),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
              child: const Text('Remove'),
            ),
          ],
        ),
      );
      if (confirmed != true || !context.mounted) return;
      final result =
          await sl<RemovePassengerUseCase>()(RemovePassengerParams(
        tripId: tripId,
        passengerId: passenger.id,
      ));
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
        (_) => onRefresh(),
      );
      return;
    }

    PassengerPaymentStatus newStatus;
    switch (action) {
      case 'paid':
        newStatus = PassengerPaymentStatus.confirmed;
      case 'cancel':
        newStatus = PassengerPaymentStatus.cancelled;
      default:
        newStatus = PassengerPaymentStatus.tentative;
    }

    final result = await sl<UpdatePassengerPaymentStatusUseCase>()(
      UpdatePassengerPaymentStatusParams(
        tripId: tripId,
        passengerId: passenger.id,
        paymentStatus: newStatus,
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
      (_) => onRefresh(),
    );
  }

  Color _statusColor(PassengerPaymentStatus status) {
    return switch (status) {
      PassengerPaymentStatus.confirmed || PassengerPaymentStatus.paid =>
        const Color(0xFF059669),
      PassengerPaymentStatus.cancelled || PassengerPaymentStatus.released =>
        AppColors.danger,
      _ => const Color(0xFFD97706),
    };
  }
}

class _StatusBadge extends StatelessWidget {
  final PassengerPaymentStatus status;
  const _StatusBadge(this.status);

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      PassengerPaymentStatus.confirmed || PassengerPaymentStatus.paid =>
        ('Confirmed', const Color(0xFF059669)),
      PassengerPaymentStatus.tentative || PassengerPaymentStatus.pending =>
        ('Tentative', const Color(0xFFD97706)),
      PassengerPaymentStatus.awaitingPayment =>
        ('Awaiting Payment', const Color(0xFFEA580C)),
      PassengerPaymentStatus.released =>
        ('Released', AppColors.danger),
      PassengerPaymentStatus.cancelled =>
        ('Cancelled', AppColors.danger),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: color,
        ),
      ),
    );
  }
}

class _AddPassengerSheet extends StatefulWidget {
  final String tripId;
  final VoidCallback onRefresh;

  const _AddPassengerSheet({required this.tripId, required this.onRefresh});

  @override
  State<_AddPassengerSheet> createState() => _AddPassengerSheetState();
}

class _AddPassengerSheetState extends State<_AddPassengerSheet> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _contactController = TextEditingController();
  final _seatController = TextEditingController();
  PassengerPaymentStatus _paymentStatus = PassengerPaymentStatus.tentative;
  bool _saving = false;

  @override
  void dispose() {
    _nameController.dispose();
    _contactController.dispose();
    _seatController.dispose();
    super.dispose();
  }

  Future<void> _confirm() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);

    final result = await sl<AddPassengerUseCase>()(AddPassengerParams(
      tripId: widget.tripId,
      name: _nameController.text.trim(),
      contactInfo: _contactController.text.trim().isEmpty
          ? null
          : _contactController.text.trim(),
      seatNumber: _seatController.text.trim().isEmpty
          ? null
          : int.tryParse(_seatController.text.trim()),
      paymentStatus: _paymentStatus,
    ));

    if (!mounted) return;
    setState(() => _saving = false);

    result.fold(
      (f) => ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(f.message), backgroundColor: AppColors.danger),
      ),
      (_) {
        widget.onRefresh();
        Navigator.of(context).pop();
      },
    );
  }

  Widget _label(String text) => Text(
        text,
        style: const TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.w600,
          color: Color(0xFF374151),
        ),
      );

  InputDecoration _inputDec(String hint) => InputDecoration(
        hintText: hint,
        isDense: true,
        filled: true,
        fillColor: const Color(0xFFF9FAFB),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
        ),
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      );

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;
    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      constraints: BoxConstraints(
        maxHeight: MediaQuery.sizeOf(context).height * 0.88,
      ),
      child: Form(
        key: _formKey,
        child: SingleChildScrollView(
          padding: EdgeInsets.fromLTRB(24, 20, 24, 24 + bottomInset),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(
                child: Container(
                  width: 40,
                  height: 4,
                  decoration: BoxDecoration(
                    color: const Color(0xFFE5E7EB),
                    borderRadius: BorderRadius.circular(2),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              const Text(
                'Add Passenger',
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                ),
              ),
              const SizedBox(height: 20),
              _label('Passenger Name *'),
              const SizedBox(height: 6),
              TextFormField(
                controller: _nameController,
                textCapitalization: TextCapitalization.words,
                decoration: _inputDec('e.g. Jane Smith'),
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? 'Name is required' : null,
              ),
              const SizedBox(height: 14),
              _label('Contact Info'),
              const SizedBox(height: 6),
              TextFormField(
                controller: _contactController,
                keyboardType: TextInputType.phone,
                decoration: _inputDec('Phone or email'),
              ),
              const SizedBox(height: 14),
              _label('Seat Number'),
              const SizedBox(height: 6),
              TextFormField(
                controller: _seatController,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                decoration: _inputDec('Auto-assigned if blank'),
              ),
              const SizedBox(height: 14),
              _label('Payment Status'),
              const SizedBox(height: 8),
              SegmentedButton<PassengerPaymentStatus>(
                selected: {_paymentStatus},
                onSelectionChanged: (s) =>
                    setState(() => _paymentStatus = s.first),
                style: SegmentedButton.styleFrom(
                  selectedBackgroundColor:
                      const Color(0xFF0F766E).withValues(alpha: 0.1),
                  selectedForegroundColor: const Color(0xFF0F766E),
                ),
                segments: const [
                  ButtonSegment(
                    value: PassengerPaymentStatus.tentative,
                    label: Text('Tentative'),
                  ),
                  ButtonSegment(
                    value: PassengerPaymentStatus.confirmed,
                    label: Text('Confirmed'),
                  ),
                ],
              ),
              const SizedBox(height: 24),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed:
                          _saving ? null : () => Navigator.of(context).pop(),
                      child: const Text('Cancel'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: FilledButton(
                      onPressed: _saving ? null : _confirm,
                      style: FilledButton.styleFrom(
                          backgroundColor: const Color(0xFF0F766E)),
                      child: _saving
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(
                                  strokeWidth: 2, color: Colors.white),
                            )
                          : const Text('Add Passenger'),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

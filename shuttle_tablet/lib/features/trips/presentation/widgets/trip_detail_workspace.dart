import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../../../drivers/domain/entities/driver.dart';
import '../../../drivers/presentation/providers/drivers_provider.dart';
import '../providers/trip_form_provider.dart';
import '../providers/trips_provider.dart';
import 'trip_cargo_manifest.dart';
import 'trip_passenger_manifest.dart';
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
                onPressed: () => context.push('/trips/${trip.id}/view'),
                icon: const Icon(Icons.open_in_full_rounded, size: 15),
                label: const Text('Full Screen'),
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColors.primary,
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  textStyle: const TextStyle(
                      fontSize: 12, fontWeight: FontWeight.w600),
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

          // Passenger manifest
          TripPassengerManifest(trip: trip, onRefresh: onRefresh),
          const SizedBox(height: 16),
          TripCargoManifest(trip: trip, onRefresh: onRefresh),
          const SizedBox(height: 20),
          const Divider(),
          const SizedBox(height: 16),

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
      await ref.read(tripFormProvider).dispatchTrip(trip.id);
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
      await ref.read(tripFormProvider).assignDriver(
        trip.id,
        const AssignDriverParams(driverId: ''),
      );
    } catch (_) {}
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

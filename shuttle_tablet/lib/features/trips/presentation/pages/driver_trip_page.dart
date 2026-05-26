import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../providers/trip_form_provider.dart';
import '../providers/trips_provider.dart';
import '../widgets/trip_status_badge.dart';
import '../widgets/trip_stop_list.dart';
import 'pre_trip_inspection_page.dart';
import 'post_trip_report_page.dart';

class DriverTripPage extends ConsumerWidget {
  final String tripId;
  const DriverTripPage({super.key, required this.tripId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripAsync = ref.watch(tripDetailProvider(tripId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          'My Trip',
          style: TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w700,
            color: AppColors.textPrimary,
          ),
        ),
        actions: [
          IconButton(
            onPressed: () => ref.invalidate(tripDetailProvider(tripId)),
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'Refresh',
          ),
        ],
      ),
      body: tripAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.error_outline_rounded,
                  size: 48, color: AppColors.danger),
              const SizedBox(height: 12),
              Text('$e'),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => ref.invalidate(tripDetailProvider(tripId)),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (trip) => _TripBody(trip: trip),
      ),
    );
  }
}

class _TripBody extends ConsumerWidget {
  final Trip trip;
  const _TripBody({required this.trip});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Status header
          Row(
            children: [
              Expanded(
                child: Text(
                  trip.purchaseOrderNumber != null
                      ? 'PO# ${trip.purchaseOrderNumber}'
                      : 'Trip',
                  style: const TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
              ),
              TripStatusBadge(trip.status),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            DateFormat('EEEE, MMM d yyyy · h:mm a')
                .format(trip.scheduledAt.toLocal()),
            style:
                const TextStyle(fontSize: 14, color: AppColors.textSecondary),
          ),
          const SizedBox(height: 20),

          // Route card
          _Card(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const _CardTitle('Route', Icons.route_rounded),
                const SizedBox(height: 12),
                TripStopList(trip.stops),
              ],
            ),
          ),
          const SizedBox(height: 16),

          // Details card
          _Card(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const _CardTitle('Details', Icons.info_outline_rounded),
                const SizedBox(height: 12),
                if (trip.vehicleType != null)
                  _Row('Vehicle', trip.vehicleType!),
                if (trip.notes != null) _Row('Notes', trip.notes!),
              ],
            ),
          ),
          const SizedBox(height: 20),

          // Action buttons driven by status
          _ActionArea(trip: trip),

          // Inspection summary (if available)
          if (trip.preInspection != null) ...[
            const SizedBox(height: 20),
            _Card(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const _CardTitle(
                      'Pre-Trip Inspection', Icons.checklist_rounded),
                  const SizedBox(height: 12),
                  _Row('Odometer Start',
                      '${trip.preInspection!.odometerStart} km'),
                  _Row(
                      'Submitted',
                      DateFormat('MMM d · h:mm a').format(
                          trip.preInspection!.submittedAt.toLocal())),
                  _Row(
                      'Items',
                      '${trip.preInspection!.items.length} checked, '
                          '${trip.preInspection!.items.where((i) => !i.passed).length} failed'),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _ActionArea extends ConsumerWidget {
  final Trip trip;
  const _ActionArea({required this.trip});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return switch (trip.status) {
      TripStatus.dispatched when trip.preInspection == null =>
        _ActionButton(
          label: 'Start Pre-Trip Inspection',
          icon: Icons.checklist_rounded,
          color: AppColors.primary,
          onPressed: () async {
            final done = await Navigator.of(context).push<bool>(
              MaterialPageRoute(
                builder: (_) => PreTripInspectionPage(tripId: trip.id),
              ),
            );
            if (done == true) {
              ref.invalidate(tripDetailProvider(trip.id));
            }
          },
        ),
      TripStatus.dispatched when trip.preInspection != null =>
        _ActionButton(
          label: 'Mark En Route',
          icon: Icons.directions_car_rounded,
          color: const Color(0xFF059669),
          onPressed: () async {
            try {
              await ref.read(tripFormProvider.notifier).submitPostReport(
                trip.id,
                const SubmitPostReportParams(
                  odometerEnd: 0,
                  hasIncident: false,
                  isReadyToInvoice: false,
                ),
              );
            } catch (_) {}
            // Use status update
            await ref
                .read(tripsProvider.notifier)
                .updateStatus(trip.id, TripStatus.enRoute);
            ref.invalidate(tripDetailProvider(trip.id));
          },
        ),
      TripStatus.enRoute =>
        _ActionButton(
          label: 'Complete Trip',
          icon: Icons.flag_rounded,
          color: const Color(0xFF059669),
          onPressed: () async {
            final done = await Navigator.of(context).push<bool>(
              MaterialPageRoute(
                builder: (_) => PostTripReportPage(tripId: trip.id),
              ),
            );
            if (done == true) {
              ref.invalidate(tripDetailProvider(trip.id));
            }
          },
        ),
      _ => const SizedBox.shrink(),
    };
  }
}

class _ActionButton extends StatelessWidget {
  final String label;
  final IconData icon;
  final Color color;
  final VoidCallback onPressed;

  const _ActionButton({
    required this.label,
    required this.icon,
    required this.color,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) => SizedBox(
        width: double.infinity,
        child: FilledButton.icon(
          onPressed: onPressed,
          icon: Icon(icon),
          label: Text(label),
          style: FilledButton.styleFrom(
            backgroundColor: color,
            padding: const EdgeInsets.symmetric(vertical: 16),
            textStyle:
                const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
          ),
        ),
      );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

class _Card extends StatelessWidget {
  final Widget child;
  const _Card({required this.child});

  @override
  Widget build(BuildContext context) => Container(
        width: double.infinity,
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: const Color(0xFFE5E7EB)),
        ),
        child: child,
      );
}

class _CardTitle extends StatelessWidget {
  final String title;
  final IconData icon;
  const _CardTitle(this.title, this.icon);

  @override
  Widget build(BuildContext context) => Row(
        children: [
          Icon(icon, size: 16, color: AppColors.primary),
          const SizedBox(width: 6),
          Text(
            title,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
        ],
      );
}

class _Row extends StatelessWidget {
  final String label;
  final String value;
  const _Row(this.label, this.value);

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 6),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 120,
              child: Text(
                label,
                style: const TextStyle(
                    fontSize: 13, color: AppColors.textSecondary),
              ),
            ),
            Expanded(
              child: Text(
                value,
                style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                    color: AppColors.textPrimary),
              ),
            ),
          ],
        ),
      );
}

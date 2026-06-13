import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../../core/constants/app_constants.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/trip.dart';
import '../../domain/entities/trip_cargo_item.dart';
import '../../domain/entities/trip_passenger.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../../domain/usecases/send_stop_update_usecase.dart';
import '../providers/trips_provider.dart';
import '../widgets/admin_trip_status_bar.dart';
import '../widgets/trip_cargo_manifest.dart';
import '../widgets/trip_passenger_manifest.dart';
import '../widgets/trip_status_badge.dart';
import '../widgets/trip_stop_progress.dart';

class AdminTripExecutionPage extends ConsumerWidget {
  final String tripId;
  const AdminTripExecutionPage({super.key, required this.tripId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripAsync = ref.watch(tripDetailProvider(tripId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          onPressed: () => context.pop(),
          icon: const Icon(Icons.arrow_back_rounded),
        ),
        title: tripAsync.maybeWhen(
          data: (trip) => Text(
            trip.purchaseOrderNumber != null
                ? 'PO# ${trip.purchaseOrderNumber}'
                : 'Trip Management',
            style: const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
          orElse: () => const Text('Trip Management',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
        ),
        actions: [
          IconButton(
            onPressed: () {
              ref.invalidate(tripDetailProvider(tripId));
              ref.invalidate(tripsProvider);
            },
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'Refresh',
          ),
        ],
      ),
      body: tripAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('$e')),
        data: (trip) {
          return _AdminTripBody(
            trip: trip,
            onRefresh: () {
              ref.invalidate(tripDetailProvider(tripId));
              ref.invalidate(tripsProvider);
            },
          );
        },
      ),
    );
  }
}

class _AdminTripBody extends ConsumerWidget {
  final Trip trip;
  final VoidCallback onRefresh;

  const _AdminTripBody({required this.trip, required this.onRefresh});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          flex: 2,
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _TripHeroCard(trip: trip),
                const SizedBox(height: 16),
                AdminTripStatusBar(trip: trip, onRefresh: onRefresh),
                const SizedBox(height: 16),
                if (trip.stops.isNotEmpty) ...[
                  _SectionCard(
                    title: 'Stop Progress',
                    icon: Icons.route_rounded,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        TripStopProgress(tripId: trip.id, stops: trip.stops),
                        if (trip.serviceType == TripServiceType.charter &&
                            trip.status != TripStatus.completed &&
                            trip.status != TripStatus.cancelled) ...[
                          const SizedBox(height: 12),
                          _SendStopUpdateButton(trip: trip),
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),
                ],
                _SectionCard(
                  title: 'Manifest Details',
                  icon: Icons.people_rounded,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      TripPassengerManifest(trip: trip, onRefresh: onRefresh),
                      const SizedBox(height: 20),
                      const Divider(),
                      const SizedBox(height: 16),
                      TripCargoManifest(trip: trip, onRefresh: onRefresh),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
        const VerticalDivider(width: 1),
        SizedBox(
          width: 300,
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _RequiredActionsPanel(trip: trip),
                const SizedBox(height: 16),
                _ContactsPanel(trip: trip),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _SectionCard extends StatelessWidget {
  final String title;
  final IconData icon;
  final Widget child;

  const _SectionCard({
    required this.title,
    required this.icon,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 16, color: AppColors.primary),
              const SizedBox(width: 6),
              Text(title,
                  style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary)),
            ],
          ),
          const SizedBox(height: 14),
          child,
        ],
      ),
    );
  }
}

class _TripHeroCard extends StatelessWidget {
  final Trip trip;
  const _TripHeroCard({required this.trip});

  @override
  Widget build(BuildContext context) {
    final sortedStops = [...trip.stops]
      ..sort((a, b) => a.sequenceOrder.compareTo(b.sequenceOrder));
    final activePax = trip.passengers
        .where((p) => p.paymentStatus != PassengerPaymentStatus.cancelled)
        .length;
    final boxCount = trip.cargoItems
        .where((c) => c.cargoType == TripCargoType.box)
        .fold<int>(0, (s, c) => s + c.quantity);
    final palletCount = trip.cargoItems
        .where((c) => c.cargoType == TripCargoType.pallet)
        .fold<int>(0, (s, c) => s + c.quantity);

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE5E7EB)),
        boxShadow: const [
          BoxShadow(
              color: Color(0x08000000), blurRadius: 12, offset: Offset(0, 4))
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: AppColors.warning.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Text(
                  _statusPill(trip.status),
                  style: const TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: AppColors.warning,
                  ),
                ),
              ),
              const Spacer(),
              TripStatusBadge(trip.status),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            trip.purchaseOrderNumber != null
                ? 'PO# ${trip.purchaseOrderNumber}'
                : '${trip.firstStopLocation ?? 'Trip'} → ${trip.lastStopLocation ?? ''}',
            style: const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              const Icon(Icons.access_time_rounded,
                  size: 15, color: AppColors.textSecondary),
              const SizedBox(width: 6),
              Text(
                'Scheduled: ${DateFormat('EEEE, MMM d · h:mm a').format(trip.scheduledAt.toLocal())}',
                style: const TextStyle(
                    fontSize: 13, color: AppColors.textSecondary),
              ),
            ],
          ),
          const SizedBox(height: 16),
          const Divider(height: 1),
          const SizedBox(height: 16),
          ...List.generate(sortedStops.length, (i) {
            final stop = sortedStops[i];
            final isLast = i == sortedStops.length - 1;
            return Padding(
              padding: EdgeInsets.only(bottom: isLast ? 0 : 8),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    isLast
                        ? Icons.flag_rounded
                        : i == 0
                            ? Icons.trip_origin
                            : Icons.circle,
                    size: 14,
                    color: isLast
                        ? AppColors.success
                        : i == 0
                            ? AppColors.primary
                            : AppColors.brandGray,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          i == 0
                              ? 'Pickup'
                              : isLast
                                  ? 'Drop-off'
                                  : 'Stop ${i + 1}',
                          style: const TextStyle(
                              fontSize: 10,
                              fontWeight: FontWeight.w600,
                              color: AppColors.textSecondary),
                        ),
                        Text(stop.locationName,
                            style: const TextStyle(
                                fontSize: 14, fontWeight: FontWeight.w600)),
                        if (stop.address != null)
                          Text(stop.address!,
                              style: const TextStyle(
                                  fontSize: 12,
                                  color: AppColors.textSecondary)),
                      ],
                    ),
                  ),
                ],
              ),
            );
          }),
          const SizedBox(height: 16),
          const Divider(height: 1),
          const SizedBox(height: 12),
          Wrap(
            spacing: 16,
            runSpacing: 6,
            children: [
              if (trip.vehicleType != null)
                _MetaChip(Icons.directions_bus_rounded, trip.vehicleType!),
              _MetaChip(Icons.people_rounded, '$activePax Pax'),
              if (boxCount > 0)
                _MetaChip(Icons.inventory_2_outlined, '$boxCount Boxes'),
              if (palletCount > 0)
                _MetaChip(Icons.view_module_rounded, '$palletCount Pallets'),
            ],
          ),
        ],
      ),
    );
  }

  String _statusPill(TripStatus s) => switch (s) {
        TripStatus.scheduled => 'SCHEDULED',
        TripStatus.dispatched => 'UP NEXT',
        TripStatus.enRoute => 'EN ROUTE',
        TripStatus.completed => 'COMPLETED',
        TripStatus.cancelled => 'CANCELLED',
      };
}

class _SendStopUpdateButton extends StatelessWidget {
  final Trip trip;
  const _SendStopUpdateButton({required this.trip});

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerLeft,
      child: OutlinedButton.icon(
        onPressed: () => _showDialog(context),
        icon: const Icon(Icons.send_rounded, size: 16),
        label: const Text('Send Stop Update'),
        style: OutlinedButton.styleFrom(
          foregroundColor: const Color(0xFF0F766E),
          side: const BorderSide(color: Color(0xFF0F766E)),
        ),
      ),
    );
  }

  Future<void> _showDialog(BuildContext context) async {
    final sortedStops = [...trip.stops]
      ..sort((a, b) => a.sequenceOrder.compareTo(b.sequenceOrder));
    String? selectedStopId =
        sortedStops.isNotEmpty ? sortedStops.first.id : null;
    final statusController = TextEditingController(text: 'On Time');

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setState) => AlertDialog(
          title: const Text('Send Stop Update'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Send a status update to this client\'s '
                'Trip Departures & Arrivals recipients.',
                style: TextStyle(fontSize: 13, color: AppColors.textSecondary),
              ),
              const SizedBox(height: 16),
              DropdownButtonFormField<String>(
                value: selectedStopId,
                decoration: const InputDecoration(
                  labelText: 'Current Stop',
                  border: OutlineInputBorder(),
                ),
                items: sortedStops
                    .map((s) => DropdownMenuItem(
                          value: s.id,
                          child: Text(s.locationName),
                        ))
                    .toList(),
                onChanged: (v) => setState(() => selectedStopId = v),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: statusController,
                decoration: const InputDecoration(
                  labelText: 'Status',
                  hintText: 'e.g. On Time, Delayed 15 min',
                  border: OutlineInputBorder(),
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              style: FilledButton.styleFrom(
                  backgroundColor: const Color(0xFF0F766E)),
              child: const Text('Send'),
            ),
          ],
        ),
      ),
    );

    if (confirmed != true || !context.mounted) return;

    final result = await sl<SendStopUpdateUseCase>()(
      SendStopUpdateParams(
        tripId: trip.id,
        stopId: selectedStopId,
        status: statusController.text.trim().isEmpty
            ? null
            : statusController.text.trim(),
      ),
    );

    if (!context.mounted) return;
    result.fold(
      (f) => ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(f.message), backgroundColor: AppColors.danger),
      ),
      (_) => ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Stop update sent.'),
          backgroundColor: Color(0xFF059669),
        ),
      ),
    );
  }
}

class _MetaChip extends StatelessWidget {
  final IconData icon;
  final String label;
  const _MetaChip(this.icon, this.label);

  @override
  Widget build(BuildContext context) => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: AppColors.textSecondary),
          const SizedBox(width: 4),
          Text(label,
              style: const TextStyle(
                  fontSize: 12, color: AppColors.textSecondary)),
        ],
      );
}

class _RequiredActionsPanel extends StatelessWidget {
  final Trip trip;
  const _RequiredActionsPanel({required this.trip});

  @override
  Widget build(BuildContext context) {
    final preDone = trip.preInspection != null;
    final postDone = trip.postReport != null;

    return _SidebarCard(
      title: 'Required Actions',
      icon: Icons.bolt_rounded,
      child: Column(
        children: [
          GestureDetector(
            onTap: () => context.push(
                '/driver/trips/${trip.id}/pre-inspection'),
            child: _ActionTile(
              title: 'Pre-Trip Inspection',
              subtitle: preDone ? 'Completed — tap to view' : 'Tap to complete',
              urgent: !preDone && trip.status == TripStatus.dispatched,
              done: preDone,
            ),
          ),
          const SizedBox(height: 8),
          _ActionTile(
            title: 'Post-Trip Report',
            subtitle: postDone
                ? 'Submitted'
                : trip.status == TripStatus.completed
                    ? 'Ready to submit'
                    : 'Available after completion',
            done: postDone,
          ),
        ],
      ),
    );
  }
}

class _ContactsPanel extends StatelessWidget {
  final Trip trip;
  const _ContactsPanel({required this.trip});

  @override
  Widget build(BuildContext context) {
    return _SidebarCard(
      title: 'Support Contacts',
      icon: Icons.headset_mic_rounded,
      child: Column(
        children: [
          _ContactTile(
            label: 'Dispatch Office',
            subtitle: 'Operations Support',
            phone: AppConstants.dispatchPhone,
          ),
          const SizedBox(height: 6),
          _ContactTile(
            label: 'Site Contact',
            subtitle: 'On-site coordinator',
            phone: AppConstants.siteContactPhone,
          ),
        ],
      ),
    );
  }
}

class _SidebarCard extends StatelessWidget {
  final String title;
  final IconData icon;
  final Widget child;

  const _SidebarCard({
    required this.title,
    required this.icon,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 15, color: AppColors.primary),
              const SizedBox(width: 5),
              Text(title,
                  style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary)),
            ],
          ),
          const SizedBox(height: 10),
          child,
        ],
      ),
    );
  }
}

class _ActionTile extends StatelessWidget {
  final String title;
  final String subtitle;
  final bool urgent;
  final bool done;

  const _ActionTile({
    required this.title,
    required this.subtitle,
    this.urgent = false,
    this.done = false,
  });

  @override
  Widget build(BuildContext context) {
    final color = done
        ? AppColors.success
        : urgent
            ? AppColors.danger
            : AppColors.brandGray;
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.05),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Row(
        children: [
          Icon(
            done ? Icons.check_circle_rounded : Icons.warning_amber_rounded,
            size: 18,
            color: color,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title,
                    style: const TextStyle(
                        fontSize: 13, fontWeight: FontWeight.w700)),
                Text(subtitle, style: TextStyle(fontSize: 11, color: color)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ContactTile extends StatelessWidget {
  final String label;
  final String subtitle;
  final String phone;

  const _ContactTile({
    required this.label,
    required this.subtitle,
    required this.phone,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () async {
        final uri = Uri(scheme: 'tel', path: phone);
        if (await canLaunchUrl(uri)) await launchUrl(uri);
      },
      child: Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: const Color(0xFFE5E7EB)),
        ),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(label,
                      style: const TextStyle(
                          fontSize: 13, fontWeight: FontWeight.w600)),
                  Text(subtitle,
                      style: const TextStyle(
                          fontSize: 11, color: AppColors.textSecondary)),
                ],
              ),
            ),
            const Icon(Icons.phone_rounded,
                size: 18, color: AppColors.brandGray),
          ],
        ),
      ),
    );
  }
}

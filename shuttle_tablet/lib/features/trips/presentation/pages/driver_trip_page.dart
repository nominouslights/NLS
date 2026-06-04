import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../../core/constants/app_constants.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/delay_entry.dart';
import '../../domain/entities/trip.dart';
import '../../domain/entities/trip_passenger.dart';
import '../providers/trips_provider.dart';
import '../widgets/trip_status_badge.dart';
import '../widgets/trip_stop_progress.dart';
import '../widgets/trip_delay_dialog.dart';

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
        leading: IconButton(
          onPressed: () => context.pop(),
          icon: const Icon(Icons.arrow_back_rounded),
        ),
        title: tripAsync.maybeWhen(
          data: (trip) => Text(
            trip.purchaseOrderNumber != null
                ? 'PO# ${trip.purchaseOrderNumber}'
                : 'Trip Execution',
            style: const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
          orElse: () => const Text('My Trip',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
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
        data: (trip) => _TripExecutionBody(trip: trip),
      ),
    );
  }
}

// ── Main body ─────────────────────────────────────────────────────────────────

class _TripExecutionBody extends ConsumerWidget {
  final Trip trip;
  const _TripExecutionBody({required this.trip});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // ── Left column ──────────────────────────────────────────────────────
        Expanded(
          flex: 2,
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _TripHeroCard(trip: trip),
                const SizedBox(height: 16),
                _StatusActionBar(trip: trip),
                const SizedBox(height: 16),
                _StopProgressSection(trip: trip),
                if (trip.serviceType == TripServiceType.community &&
                    trip.passengers.isNotEmpty) ...[
                  const SizedBox(height: 16),
                  _ManifestSection(trip: trip),
                ],
              ],
            ),
          ),
        ),
        const VerticalDivider(width: 1),
        // ── Right sidebar ────────────────────────────────────────────────────
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
                const SizedBox(height: 16),
                _DelayLogPanel(trip: trip),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

// ── Trip Hero Card ────────────────────────────────────────────────────────────

class _TripHeroCard extends StatelessWidget {
  final Trip trip;
  const _TripHeroCard({required this.trip});

  @override
  Widget build(BuildContext context) {
    final sortedStops = [...trip.stops]
      ..sort((a, b) => a.sequenceOrder.compareTo(b.sequenceOrder));

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
          // Header row
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: AppColors.warning.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(
                      color: AppColors.warning.withValues(alpha: 0.25)),
                ),
                child: Text(
                  _statusPill(trip.status),
                  style: const TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: AppColors.warning,
                    letterSpacing: 0.4,
                  ),
                ),
              ),
              const Spacer(),
              TripStatusBadge(trip.status),
            ],
          ),
          const SizedBox(height: 12),
          // Scheduled time
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
          // Route summary
          ...List.generate(sortedStops.length, (i) {
            final stop = sortedStops[i];
            final isLast = i == sortedStops.length - 1;
            return Column(
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Column(
                      children: [
                        Container(
                          width: 10,
                          height: 10,
                          decoration: BoxDecoration(
                            shape: isLast ? BoxShape.rectangle : BoxShape.circle,
                            color: isLast
                                ? AppColors.success
                                : i == 0
                                    ? AppColors.primary
                                    : AppColors.brandGray,
                            borderRadius:
                                isLast ? BorderRadius.circular(2) : null,
                          ),
                        ),
                        if (!isLast)
                          Container(
                              width: 2, height: 24, color: const Color(0xFFE5E7EB)),
                      ],
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Padding(
                        padding: EdgeInsets.only(bottom: isLast ? 0 : 8),
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
                            Text(
                              stop.locationName,
                              style: const TextStyle(
                                  fontSize: 14,
                                  fontWeight: FontWeight.w600,
                                  color: AppColors.textPrimary),
                            ),
                            if (stop.address != null)
                              Text(stop.address!,
                                  style: const TextStyle(
                                      fontSize: 12,
                                      color: AppColors.textSecondary)),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            );
          }),
          const SizedBox(height: 16),
          const Divider(height: 1),
          const SizedBox(height: 12),
          // Footer meta
          Wrap(
            spacing: 16,
            runSpacing: 6,
            children: [
              if (trip.vehicleType != null)
                _MetaChip(Icons.directions_bus_rounded, trip.vehicleType!),
              if (trip.serviceType == TripServiceType.community &&
                  trip.seatCapacity != null)
                _MetaChip(Icons.people_rounded,
                    '${trip.passengers.length} / ${trip.seatCapacity} pax'),
              if (trip.notes != null)
                _MetaChip(Icons.notes_rounded, trip.notes!),
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

// ── Status Action Bar ─────────────────────────────────────────────────────────

class _StatusActionBar extends ConsumerWidget {
  final Trip trip;
  const _StatusActionBar({required this.trip});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final stopIndex = ref.watch(currentStopIndexProvider(trip.id));
    final totalStops = trip.stops.length;
    final isLastStop = stopIndex >= totalStops - 1;

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
          const Row(
            children: [
              Icon(Icons.bolt_rounded, size: 16, color: AppColors.warning),
              SizedBox(width: 6),
              Text('Status Actions',
                  style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary)),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: _buildButtons(context, ref, stopIndex, isLastStop),
          ),
        ],
      ),
    );
  }

  List<Widget> _buildButtons(
      BuildContext context, WidgetRef ref, int stopIndex, bool isLastStop) {
    final buttons = <Widget>[];

    switch (trip.status) {
      case TripStatus.dispatched:
        if (trip.preInspection == null) {
          buttons.add(_ActionBtn(
            label: 'Start Pre-Trip',
            icon: Icons.checklist_rounded,
            color: AppColors.primary,
            onPressed: () => context.push('/driver/trips/${trip.id}/pre-inspection'),
          ));
        } else {
          buttons.add(_ActionBtn(
            label: 'Arrived at Pickup',
            icon: Icons.location_on_rounded,
            color: AppColors.primary,
            onPressed: () async {
              try {
                await ref
                    .read(tripsProvider.notifier)
                    .updateStatus(trip.id, TripStatus.enRoute);
                ref.invalidate(tripDetailProvider(trip.id));
              } catch (e) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                        content: Text('Failed: $e'),
                        backgroundColor: AppColors.danger),
                  );
                }
              }
            },
          ));
        }

      case TripStatus.enRoute:
        if (!isLastStop) {
          buttons.add(_ActionBtn(
            label: 'Departed Stop',
            icon: Icons.play_arrow_rounded,
            color: AppColors.primary,
            onPressed: () {
              ref.read(currentStopIndexProvider(trip.id).notifier).state =
                  stopIndex + 1;
            },
          ));
        } else {
          buttons.add(_ActionBtn(
            label: 'Mark Complete',
            icon: Icons.flag_rounded,
            color: AppColors.success,
            onPressed: () {
              final delays = ref.read(tripDelayLogsProvider(trip.id));
              DelayHandoff? handoff;
              if (delays.isNotEmpty) {
                final first = delays.first;
                handoff = DelayHandoff(
                    type: first.type, description: first.description);
              }
              context.push('/driver/trips/${trip.id}/post-report',
                  extra: handoff);
            },
          ));
        }

      case TripStatus.completed:
        buttons.add(_ActionBtn(
          label: 'Post-Trip Report',
          icon: Icons.file_present_rounded,
          color: AppColors.primary,
          onPressed: () => context.push('/driver/trips/${trip.id}/post-report'),
        ));

      case TripStatus.scheduled:
      case TripStatus.cancelled:
        break;
    }

    // Report delay always visible for active trips
    if (trip.status == TripStatus.dispatched ||
        trip.status == TripStatus.enRoute) {
      buttons.add(_ActionBtn(
        label: 'Report Delay',
        icon: Icons.warning_amber_rounded,
        color: AppColors.warning,
        outlined: true,
        onPressed: () async {
          final entry = await showTripDelayDialog(context);
          if (entry == null) return;
          ref.read(tripDelayLogsProvider(trip.id).notifier).update(
                (list) => [...list, entry],
              );
          if (context.mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Delay logged')),
            );
          }
        },
      ));
    }

    return buttons;
  }
}

class _ActionBtn extends StatelessWidget {
  final String label;
  final IconData icon;
  final Color color;
  final VoidCallback onPressed;
  final bool outlined;

  const _ActionBtn({
    required this.label,
    required this.icon,
    required this.color,
    required this.onPressed,
    this.outlined = false,
  });

  @override
  Widget build(BuildContext context) {
    if (outlined) {
      return OutlinedButton.icon(
        onPressed: onPressed,
        icon: Icon(icon, size: 16),
        label: Text(label),
        style: OutlinedButton.styleFrom(
          foregroundColor: color,
          side: BorderSide(color: color),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
          textStyle: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
        ),
      );
    }
    return FilledButton.icon(
      onPressed: onPressed,
      icon: Icon(icon, size: 16),
      label: Text(label),
      style: FilledButton.styleFrom(
        backgroundColor: color,
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        textStyle: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700),
      ),
    );
  }
}

// ── Stop Progress Section ─────────────────────────────────────────────────────

class _StopProgressSection extends StatelessWidget {
  final Trip trip;
  const _StopProgressSection({required this.trip});

  @override
  Widget build(BuildContext context) {
    if (trip.stops.isEmpty) return const SizedBox.shrink();
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
          const Row(
            children: [
              Icon(Icons.route_rounded, size: 16, color: AppColors.primary),
              SizedBox(width: 6),
              Text('Stop Progress',
                  style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary)),
            ],
          ),
          const SizedBox(height: 14),
          TripStopProgress(tripId: trip.id, stops: trip.stops),
        ],
      ),
    );
  }
}

// ── Manifest Section (community trips) ───────────────────────────────────────

class _ManifestSection extends StatelessWidget {
  final Trip trip;
  const _ManifestSection({required this.trip});

  @override
  Widget build(BuildContext context) {
    final active = trip.passengers
        .where((p) => p.paymentStatus != PassengerPaymentStatus.cancelled)
        .toList();

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
              const Icon(Icons.people_rounded,
                  size: 16, color: AppColors.primary),
              const SizedBox(width: 6),
              Text(
                'Passenger Manifest (${active.length} / ${trip.seatCapacity ?? '?'})',
                style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary),
              ),
            ],
          ),
          const SizedBox(height: 12),
          ...active.map((p) => _PassengerRow(passenger: p)),
        ],
      ),
    );
  }
}

class _PassengerRow extends StatelessWidget {
  final TripPassenger passenger;
  const _PassengerRow({required this.passenger});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        children: [
          CircleAvatar(
            radius: 16,
            backgroundColor: AppColors.primary.withValues(alpha: 0.1),
            child: Text(
              passenger.name.isNotEmpty ? passenger.name[0].toUpperCase() : '?',
              style: const TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  color: AppColors.primary),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(passenger.name,
                    style: const TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary)),
                if (passenger.seatNumber != null)
                  Text('Seat ${passenger.seatNumber}',
                      style: const TextStyle(
                          fontSize: 11, color: AppColors.textSecondary)),
              ],
            ),
          ),
          _PayBadge(passenger.paymentStatus),
        ],
      ),
    );
  }
}

class _PayBadge extends StatelessWidget {
  final PassengerPaymentStatus status;
  const _PayBadge(this.status);

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      PassengerPaymentStatus.confirmed || PassengerPaymentStatus.paid =>
        ('Confirmed', AppColors.success),
      PassengerPaymentStatus.tentative || PassengerPaymentStatus.pending =>
        ('Tentative', AppColors.warning),
      _ => ('Pending', AppColors.brandGray),
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
              fontSize: 11, fontWeight: FontWeight.w600, color: color)),
    );
  }
}

// ── Required Actions Panel ────────────────────────────────────────────────────

class _RequiredActionsPanel extends StatelessWidget {
  final Trip trip;
  const _RequiredActionsPanel({required this.trip});

  @override
  Widget build(BuildContext context) {
    final preInspectionDone = trip.preInspection != null;
    final postReportDone = trip.postReport != null;
    final canAccessPostReport = trip.status == TripStatus.completed;

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
          const Row(
            children: [
              Icon(Icons.bolt_rounded, size: 15, color: AppColors.warning),
              SizedBox(width: 5),
              Text('Required Actions',
                  style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary)),
            ],
          ),
          const SizedBox(height: 10),
          // Pre-trip card
          _ActionCard(
            title: 'Pre-Trip Inspection',
            icon: Icons.checklist_rounded,
            done: preInspectionDone,
            urgent: !preInspectionDone &&
                trip.status == TripStatus.dispatched,
            subtitle: preInspectionDone
                ? 'Completed'
                : 'Required before departure',
            onTap: !preInspectionDone
                ? () => context.push(
                    '/driver/trips/${trip.id}/pre-inspection')
                : null,
          ),
          const SizedBox(height: 8),
          // Post-trip card
          _ActionCard(
            title: 'Post-Trip Report',
            icon: Icons.file_present_rounded,
            done: postReportDone,
            urgent: false,
            subtitle: postReportDone
                ? 'Submitted'
                : canAccessPostReport
                    ? 'Ready to submit'
                    : 'Available after completion',
            onTap: canAccessPostReport && !postReportDone
                ? () =>
                    context.push('/driver/trips/${trip.id}/post-report')
                : null,
          ),
        ],
      ),
    );
  }
}

class _ActionCard extends StatelessWidget {
  final String title;
  final IconData icon;
  final bool done;
  final bool urgent;
  final String subtitle;
  final VoidCallback? onTap;

  const _ActionCard({
    required this.title,
    required this.icon,
    required this.done,
    required this.urgent,
    required this.subtitle,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    Color borderColor;
    Color bgColor;
    Color iconColor;

    if (done) {
      borderColor = AppColors.success.withValues(alpha: 0.3);
      bgColor = AppColors.success.withValues(alpha: 0.05);
      iconColor = AppColors.success;
    } else if (urgent) {
      borderColor = AppColors.danger.withValues(alpha: 0.4);
      bgColor = AppColors.danger.withValues(alpha: 0.05);
      iconColor = AppColors.danger;
    } else if (onTap != null) {
      borderColor = AppColors.primary.withValues(alpha: 0.3);
      bgColor = AppColors.primary.withValues(alpha: 0.04);
      iconColor = AppColors.primary;
    } else {
      borderColor = const Color(0xFFE5E7EB);
      bgColor = const Color(0xFFF9FAFB);
      iconColor = AppColors.brandGray;
    }

    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: bgColor,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: borderColor),
        ),
        child: Row(
          children: [
            Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: borderColor),
              ),
              child: Icon(icon, size: 18, color: iconColor),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title,
                      style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w700,
                          color: onTap != null || done
                              ? AppColors.textPrimary
                              : AppColors.brandGray)),
                  Text(subtitle,
                      style: TextStyle(
                          fontSize: 11,
                          color: urgent
                              ? AppColors.danger
                              : done
                                  ? AppColors.success
                                  : AppColors.brandGray)),
                ],
              ),
            ),
            if (done)
              const Icon(Icons.check_circle_rounded,
                  color: AppColors.success, size: 18)
            else if (onTap != null)
              const Icon(Icons.chevron_right_rounded,
                  color: AppColors.brandGray, size: 18),
          ],
        ),
      ),
    );
  }
}

// ── Contacts Panel ────────────────────────────────────────────────────────────

class _ContactsPanel extends StatelessWidget {
  final Trip trip;
  const _ContactsPanel({required this.trip});

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
          const Row(
            children: [
              Icon(Icons.headset_mic_rounded, size: 15, color: AppColors.primary),
              SizedBox(width: 5),
              Text('Support Contacts',
                  style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary)),
            ],
          ),
          const SizedBox(height: 10),
          _ContactRow(
            label: 'Dispatch Office',
            subtitle: 'Operations Support',
            phone: AppConstants.dispatchPhone,
            iconColor: AppColors.secondary,
          ),
          const SizedBox(height: 6),
          _ContactRow(
            label: trip.purchaseOrderNumber != null
                ? 'Client / Site'
                : 'Site Contact',
            subtitle: 'On-site coordinator',
            phone: AppConstants.siteContactPhone,
            iconColor: AppColors.warning,
          ),
        ],
      ),
    );
  }
}

class _ContactRow extends StatelessWidget {
  final String label;
  final String subtitle;
  final String phone;
  final Color iconColor;

  const _ContactRow({
    required this.label,
    required this.subtitle,
    required this.phone,
    required this.iconColor,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () async {
        final uri = Uri(scheme: 'tel', path: phone);
        if (await canLaunchUrl(uri)) {
          await launchUrl(uri);
        }
      },
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: const Color(0xFFE5E7EB)),
        ),
        child: Row(
          children: [
            Container(
              width: 32,
              height: 32,
              decoration: BoxDecoration(
                color: iconColor.withValues(alpha: 0.1),
                shape: BoxShape.circle,
              ),
              child: Icon(Icons.business_rounded, size: 16, color: iconColor),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(label,
                      style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary)),
                  Text(subtitle,
                      style: const TextStyle(
                          fontSize: 11, color: AppColors.textSecondary)),
                ],
              ),
            ),
            Icon(Icons.phone_rounded, size: 18, color: AppColors.brandGray),
          ],
        ),
      ),
    );
  }
}

// ── Delay Log Panel ───────────────────────────────────────────────────────────

class _DelayLogPanel extends ConsumerWidget {
  final Trip trip;
  const _DelayLogPanel({required this.trip});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final delays = ref.watch(tripDelayLogsProvider(trip.id));
    final canLogDelay = trip.status == TripStatus.dispatched ||
        trip.status == TripStatus.enRoute;

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
              const Icon(Icons.warning_amber_rounded,
                  size: 15, color: AppColors.warning),
              const SizedBox(width: 5),
              const Text('Delay Log',
                  style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary)),
              const Spacer(),
              if (canLogDelay)
                TextButton.icon(
                  onPressed: () async {
                    final entry = await showTripDelayDialog(context);
                    if (entry == null) return;
                    ref
                        .read(tripDelayLogsProvider(trip.id).notifier)
                        .update((list) => [...list, entry]);
                  },
                  icon: const Icon(Icons.add_rounded, size: 14),
                  label: const Text('Add'),
                  style: TextButton.styleFrom(
                    foregroundColor: AppColors.warning,
                    padding: EdgeInsets.zero,
                    textStyle: const TextStyle(
                        fontSize: 12, fontWeight: FontWeight.w600),
                  ),
                ),
            ],
          ),
          if (delays.isEmpty) ...[
            const SizedBox(height: 10),
            const Center(
              child: Text('No delays logged',
                  style: TextStyle(
                      fontSize: 12, color: AppColors.brandGray)),
            ),
          ] else ...[
            const SizedBox(height: 8),
            ...delays.map((d) => _DelayEntry(entry: d)),
          ],
        ],
      ),
    );
  }
}

class _DelayEntry extends StatelessWidget {
  final DelayEntry entry;
  const _DelayEntry({required this.entry});

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 6),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: AppColors.warning.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: AppColors.warning.withValues(alpha: 0.2)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(
                DateFormat('h:mm a').format(entry.loggedAt),
                style: const TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w600,
                    color: AppColors.warning),
              ),
              const SizedBox(width: 6),
              if (entry.estimatedMinutes > 0)
                Text(
                  '~${entry.estimatedMinutes} min',
                  style: const TextStyle(
                      fontSize: 11, color: AppColors.textSecondary),
                ),
            ],
          ),
          const SizedBox(height: 2),
          Text(
            entry.description,
            style: const TextStyle(
                fontSize: 12, color: AppColors.textPrimary),
          ),
        ],
      ),
    );
  }
}

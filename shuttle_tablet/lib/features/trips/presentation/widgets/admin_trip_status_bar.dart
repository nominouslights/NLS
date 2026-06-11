import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip.dart';
import '../providers/trip_form_provider.dart';
import '../providers/trips_provider.dart';

class AdminTripStatusBar extends ConsumerWidget {
  final Trip trip;
  final VoidCallback onRefresh;

  const AdminTripStatusBar({
    super.key,
    required this.trip,
    required this.onRefresh,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (trip.status == TripStatus.completed ||
        trip.status == TripStatus.cancelled) {
      return const SizedBox.shrink();
    }

    final buttons = <Widget>[];

    if (trip.status == TripStatus.scheduled && trip.driverId != null) {
      buttons.add(_ActionBtn(
        label: 'Dispatch',
        icon: Icons.send_rounded,
        color: AppColors.primary,
        onPressed: () => _dispatch(context, ref),
      ));
    }

    if (trip.status == TripStatus.dispatched) {
      buttons.add(_ActionBtn(
        label: 'Set En Route',
        icon: Icons.play_arrow_rounded,
        color: AppColors.primary,
        onPressed: () => _setStatus(context, ref, TripStatus.enRoute),
      ));
    }

    if (trip.status == TripStatus.scheduled ||
        trip.status == TripStatus.dispatched) {
      buttons.add(_ActionBtn(
        label: 'Cancel Trip',
        icon: Icons.cancel_outlined,
        color: AppColors.danger,
        outlined: true,
        onPressed: () => _cancel(context, ref),
      ));
    }

    if (buttons.isEmpty) return const SizedBox.shrink();

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
          Wrap(spacing: 8, runSpacing: 8, children: buttons),
        ],
      ),
    );
  }

  Future<void> _dispatch(BuildContext context, WidgetRef ref) async {
    try {
      await ref.read(tripFormProvider).dispatchTrip(trip.id);
      onRefresh();
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Trip dispatched')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('$e'), backgroundColor: AppColors.danger),
        );
      }
    }
  }

  Future<void> _setStatus(
      BuildContext context, WidgetRef ref, TripStatus status) async {
    try {
      await ref.read(tripsProvider.notifier).updateStatus(trip.id, status);
      onRefresh();
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Status updated to ${_label(status)}')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('$e'), backgroundColor: AppColors.danger),
        );
      }
    }
  }

  Future<void> _cancel(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Cancel Trip'),
        content: const Text('Cancel this trip?'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('No')),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
            child: const Text('Yes, Cancel'),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;
    await _setStatus(context, ref, TripStatus.cancelled);
  }

  String _label(TripStatus s) => switch (s) {
        TripStatus.scheduled => 'Scheduled',
        TripStatus.dispatched => 'Dispatched',
        TripStatus.enRoute => 'En Route',
        TripStatus.completed => 'Completed',
        TripStatus.cancelled => 'Cancelled',
      };
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
        ),
      );
    }
    return FilledButton.icon(
      onPressed: onPressed,
      icon: Icon(icon, size: 16),
      label: Text(label),
      style: FilledButton.styleFrom(backgroundColor: color),
    );
  }
}

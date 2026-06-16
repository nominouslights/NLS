import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip_stop.dart';
import '../providers/trips_provider.dart';

class TripStopProgress extends ConsumerWidget {
  final String tripId;
  final List<TripStop> stops;
  final bool isEnRoute;

  const TripStopProgress({
    super.key,
    required this.tripId,
    required this.stops,
    this.isEnRoute = false,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sorted = [...stops]..sort((a, b) => a.sequenceOrder.compareTo(b.sequenceOrder));

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: List.generate(sorted.length, (i) {
        final stop = sorted[i];
        final isLast = i == sorted.length - 1;

        final bool isDone;
        final bool isAtStop;
        final bool isActive;

        if (isLast) {
          isDone = stop.arrivedAt != null;
          isAtStop = false;
          isActive = !isDone && isEnRoute && _allPriorDone(sorted, i);
        } else {
          isDone = stop.departedAt != null;
          isAtStop = stop.arrivedAt != null && stop.departedAt == null;
          isActive = stop.arrivedAt == null && isEnRoute && _allPriorDone(sorted, i);
        }

        return IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              SizedBox(
                width: 32,
                child: Column(
                  children: [
                    _StopDot(isDone: isDone, isCurrent: isAtStop || isActive, isLast: isLast),
                    if (!isLast)
                      Expanded(
                        child: Container(
                          width: 2,
                          color: isDone ? AppColors.success : const Color(0xFFE5E7EB),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Padding(
                  padding: EdgeInsets.only(bottom: isLast ? 0 : 16),
                  child: _StopCard(
                    stop: stop,
                    isDone: isDone,
                    isAtStop: isAtStop,
                    isActive: isActive,
                    onArrived: isActive
                        ? () => _handleArrived(context, ref, stop)
                        : null,
                    onDeparted: isAtStop && isEnRoute
                        ? () => _handleDeparted(context, ref, stop)
                        : null,
                  ),
                ),
              ),
            ],
          ),
        );
      }),
    );
  }

  static bool _allPriorDone(List<TripStop> sorted, int i) {
    if (i == 0) return true;
    for (int j = 0; j < i; j++) {
      final prior = sorted[j];
      final priorIsLast = j == sorted.length - 1;
      final done = priorIsLast ? prior.arrivedAt != null : prior.departedAt != null;
      if (!done) return false;
    }
    return true;
  }

  Future<void> _handleArrived(
      BuildContext context, WidgetRef ref, TripStop stop) async {
    try {
      await ref.read(tripsProvider.notifier).markStopArrived(tripId, stop.id);
      ref.invalidate(tripDetailProvider(tripId));
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Failed to mark arrived: $e'),
            backgroundColor: AppColors.danger,
          ),
        );
      }
    }
  }

  Future<void> _handleDeparted(
      BuildContext context, WidgetRef ref, TripStop stop) async {
    try {
      await ref.read(tripsProvider.notifier).markStopDeparted(tripId, stop.id);
      ref.invalidate(tripDetailProvider(tripId));
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Failed to mark departed: $e'),
            backgroundColor: AppColors.danger,
          ),
        );
      }
    }
  }
}

class _StopDot extends StatelessWidget {
  final bool isDone;
  final bool isCurrent;
  final bool isLast;

  const _StopDot({required this.isDone, required this.isCurrent, required this.isLast});

  @override
  Widget build(BuildContext context) {
    if (isDone) {
      return Container(
        width: 24,
        height: 24,
        decoration: const BoxDecoration(
          color: AppColors.success,
          shape: BoxShape.circle,
        ),
        child: const Icon(Icons.check_rounded, color: Colors.white, size: 14),
      );
    }
    if (isCurrent) {
      return Container(
        width: 24,
        height: 24,
        decoration: BoxDecoration(
          color: AppColors.primary,
          shape: BoxShape.circle,
          boxShadow: [
            BoxShadow(
              color: AppColors.primary.withValues(alpha: 0.35),
              blurRadius: 8,
              spreadRadius: 2,
            ),
          ],
        ),
        child: const Icon(Icons.location_on_rounded, color: Colors.white, size: 14),
      );
    }
    return Container(
      width: 24,
      height: 24,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(color: const Color(0xFFD1D5DB), width: 2),
        color: Colors.white,
      ),
    );
  }
}

class _StopCard extends StatelessWidget {
  final TripStop stop;
  final bool isDone;
  final bool isAtStop;
  final bool isActive;
  final VoidCallback? onArrived;
  final VoidCallback? onDeparted;

  const _StopCard({
    required this.stop,
    required this.isDone,
    required this.isAtStop,
    required this.isActive,
    this.onArrived,
    this.onDeparted,
  });

  @override
  Widget build(BuildContext context) {
    final isCurrent = isAtStop || isActive;
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: isCurrent
            ? AppColors.primary.withValues(alpha: 0.06)
            : isDone
                ? AppColors.success.withValues(alpha: 0.05)
                : Colors.white,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(
          color: isCurrent
              ? AppColors.primary.withValues(alpha: 0.3)
              : isDone
                  ? AppColors.success.withValues(alpha: 0.25)
                  : const Color(0xFFE5E7EB),
        ),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  _stopLabel(stop.sequenceOrder),
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w700,
                    color: isCurrent ? AppColors.primary : AppColors.textSecondary,
                    letterSpacing: 0.5,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  stop.locationName,
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                if (stop.address != null)
                  Text(
                    stop.address!,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
              ],
            ),
          ),
          if (isActive && onArrived != null)
            FilledButton.icon(
              onPressed: onArrived,
              icon: const Icon(Icons.location_on_rounded, size: 14),
              label: const Text('Arrived'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                textStyle: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700),
              ),
            ),
          if (isAtStop && onDeparted != null)
            FilledButton.icon(
              onPressed: onDeparted,
              icon: const Icon(Icons.play_arrow_rounded, size: 14),
              label: const Text('Departed'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.warning,
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                textStyle: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700),
              ),
            ),
          if (isDone)
            const Icon(Icons.check_circle_rounded, color: AppColors.success, size: 20),
        ],
      ),
    );
  }

  String _stopLabel(int seq) {
    if (seq == 1) return 'PICKUP';
    return 'STOP $seq';
  }
}

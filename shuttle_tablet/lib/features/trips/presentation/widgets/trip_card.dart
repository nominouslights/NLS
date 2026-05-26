import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip.dart';
import 'trip_status_badge.dart';

class TripCard extends StatelessWidget {
  final Trip trip;
  final VoidCallback onTap;
  final bool selected;

  const TripCard({
    super.key,
    required this.trip,
    required this.onTap,
    this.selected = false,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
        decoration: BoxDecoration(
          color: selected ? const Color(0xFFEBF0FA) : Colors.white,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: selected ? AppColors.primary : const Color(0xFFE5E7EB),
            width: selected ? 1.5 : 1,
          ),
          boxShadow: selected
              ? null
              : [
                  const BoxShadow(
                    color: Color(0x0A000000),
                    blurRadius: 4,
                    offset: Offset(0, 2),
                  ),
                ],
        ),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Header row
              Row(
                children: [
                  Expanded(
                    child: Text(
                      _routeLabel,
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                  const SizedBox(width: 8),
                  TripStatusBadge(trip.status),
                ],
              ),
              const SizedBox(height: 8),
              // PO + stops count row
              Row(
                children: [
                  const Icon(Icons.route_rounded, size: 14, color: AppColors.brandGray),
                  const SizedBox(width: 4),
                  Text(
                    '${trip.stops.length} stop${trip.stops.length == 1 ? '' : 's'}',
                    style: const TextStyle(fontSize: 12, color: AppColors.brandGray),
                  ),
                  if (trip.purchaseOrderNumber != null) ...[
                    const SizedBox(width: 12),
                    const Icon(Icons.receipt_long_rounded, size: 14, color: AppColors.brandGray),
                    const SizedBox(width: 4),
                    Text(
                      'PO# ${trip.purchaseOrderNumber}',
                      style: const TextStyle(fontSize: 12, color: AppColors.brandGray),
                    ),
                  ],
                ],
              ),
              const SizedBox(height: 6),
              // Scheduled time
              Row(
                children: [
                  const Icon(Icons.schedule_rounded, size: 14, color: AppColors.brandGray),
                  const SizedBox(width: 4),
                  Text(
                    DateFormat('MMM d, yyyy · h:mm a').format(trip.scheduledAt.toLocal()),
                    style: const TextStyle(fontSize: 12, color: AppColors.textSecondary),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  String get _routeLabel {
    final first = trip.firstStopLocation;
    final last = trip.lastStopLocation;
    if (first != null && last != null) return '$first → $last';
    if (first != null) return first;
    return 'Trip #${trip.id.substring(0, 8)}';
  }
}

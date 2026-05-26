import 'package:flutter/material.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip_stop.dart';

class TripStopList extends StatelessWidget {
  final List<TripStop> stops;
  const TripStopList(this.stops, {super.key});

  @override
  Widget build(BuildContext context) {
    if (stops.isEmpty) {
      return const Text('No stops', style: TextStyle(color: AppColors.brandGray));
    }
    return Column(
      children: List.generate(stops.length, (i) {
        final stop = stops[i];
        final isFirst = i == 0;
        final isLast = i == stops.length - 1;
        return IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Timeline column
              SizedBox(
                width: 24,
                child: Column(
                  children: [
                    // Line above the dot (not for first)
                    if (!isFirst)
                      Expanded(
                        child: Center(
                          child: Container(
                            width: 2,
                            color: const Color(0xFFD1D5DB),
                          ),
                        ),
                      ),
                    _StopDot(isFirst: isFirst, isLast: isLast),
                    // Line below the dot (not for last)
                    if (!isLast)
                      Expanded(
                        child: Center(
                          child: Container(
                            width: 2,
                            color: const Color(0xFFD1D5DB),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              // Content
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 8),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        isFirst ? 'Pickup' : isLast ? 'Drop-off' : 'Stop ${stop.sequenceOrder}',
                        style: TextStyle(
                          fontSize: 10,
                          fontWeight: FontWeight.w600,
                          color: isFirst
                              ? AppColors.primary
                              : isLast
                                  ? const Color(0xFF059669)
                                  : AppColors.brandGray,
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
                      if (stop.address != null) ...[
                        const SizedBox(height: 2),
                        Text(
                          stop.address!,
                          style: const TextStyle(
                            fontSize: 12,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ],
          ),
        );
      }),
    );
  }
}

class _StopDot extends StatelessWidget {
  final bool isFirst;
  final bool isLast;
  const _StopDot({required this.isFirst, required this.isLast});

  @override
  Widget build(BuildContext context) {
    if (isFirst) {
      return Container(
        width: 12,
        height: 12,
        decoration: BoxDecoration(
          color: AppColors.primary,
          shape: BoxShape.circle,
          border: Border.all(color: AppColors.primary, width: 2),
        ),
      );
    }
    if (isLast) {
      return Container(
        width: 12,
        height: 12,
        decoration: BoxDecoration(
          color: const Color(0xFF059669),
          shape: BoxShape.circle,
          border: Border.all(color: const Color(0xFF059669), width: 2),
        ),
      );
    }
    return Container(
      width: 10,
      height: 10,
      decoration: BoxDecoration(
        color: Colors.white,
        shape: BoxShape.circle,
        border: Border.all(color: const Color(0xFFD1D5DB), width: 2),
      ),
    );
  }
}

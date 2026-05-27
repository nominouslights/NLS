import 'package:flutter/material.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/vehicle.dart';
import 'vehicle_status_badge.dart';

class VehicleCard extends StatelessWidget {
  final Vehicle vehicle;
  final VoidCallback onTap;
  final VoidCallback onEdit;
  final VoidCallback onSetOutOfService;
  final VoidCallback onChangeStatus;
  final VoidCallback onDelete;

  const VehicleCard({
    super.key,
    required this.vehicle,
    required this.onTap,
    required this.onEdit,
    required this.onSetOutOfService,
    required this.onChangeStatus,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    final statusColor = VehicleStatusBadge.colorForStatus(vehicle.status);

    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: Color(0xFFE5E7EB)),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              // Avatar — bus icon tinted by status color
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: statusColor.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                      color: statusColor.withValues(alpha: 0.25)),
                ),
                child: Center(
                  child: Icon(
                    Icons.directions_bus_rounded,
                    size: 24,
                    color: statusColor,
                  ),
                ),
              ),
              const SizedBox(width: 12),
              // Content
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        // Unit code chip
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 6, vertical: 2),
                          decoration: BoxDecoration(
                            color: AppColors.primary.withValues(alpha: 0.08),
                            borderRadius: BorderRadius.circular(6),
                          ),
                          child: Text(
                            vehicle.unitCode,
                            style: const TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                              color: AppColors.primary,
                            ),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Flexible(
                          child: Text(
                            vehicle.displayName,
                            style: const TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                              color: Color(0xFF111827),
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        const SizedBox(width: 6),
                        VehicleStatusBadge(status: vehicle.status),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Row(
                      children: [
                        Text(
                          vehicle.licensePlate,
                          style: const TextStyle(
                              fontSize: 13, color: AppColors.brandGray),
                        ),
                        if (vehicle.readinessScore < 80) ...[
                          const SizedBox(width: 8),
                          _ReadinessChip(score: vehicle.readinessScore),
                        ],
                      ],
                    ),
                    // Out-of-service reason sub-label
                    if (vehicle.isOutOfService &&
                        vehicle.statusNote != null) ...[
                      const SizedBox(height: 3),
                      Row(
                        children: [
                          const Icon(Icons.block_rounded,
                              size: 12, color: AppColors.brandGray),
                          const SizedBox(width: 4),
                          Flexible(
                            child: Text(
                              vehicle.statusNote!,
                              style: const TextStyle(
                                fontSize: 11,
                                color: AppColors.brandGray,
                              ),
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
              // Actions menu
              PopupMenuButton<String>(
                icon: const Icon(Icons.more_vert_rounded,
                    color: AppColors.brandGray),
                onSelected: (value) {
                  if (value == 'edit') onEdit();
                  if (value == 'out_of_service') onSetOutOfService();
                  if (value == 'change_status') onChangeStatus();
                  if (value == 'delete') onDelete();
                },
                itemBuilder: (_) => const [
                  PopupMenuItem(value: 'edit', child: Text('Edit')),
                  PopupMenuItem(
                      value: 'out_of_service',
                      child: Text('Set Out of Service')),
                  PopupMenuItem(
                      value: 'change_status', child: Text('Change Status')),
                  PopupMenuItem(
                    value: 'delete',
                    child: Text('Delete',
                        style: TextStyle(color: AppColors.danger)),
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

class _ReadinessChip extends StatelessWidget {
  final int score;
  const _ReadinessChip({required this.score});

  @override
  Widget build(BuildContext context) {
    final color = score >= 80
        ? AppColors.success
        : score >= 60
            ? AppColors.warning
            : AppColors.danger;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.speed_rounded, size: 10, color: color),
          const SizedBox(width: 3),
          Text(
            '$score%',
            style: TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w700,
              color: color,
            ),
          ),
        ],
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/vehicle_service_record.dart';

class ServiceRecordCard extends StatelessWidget {
  final VehicleServiceRecord record;
  final VoidCallback? onComplete;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  const ServiceRecordCard({
    super.key,
    required this.record,
    this.onComplete,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(
          color: record.isOverdue
              ? const Color(0xFFFFE0A0)
              : const Color(0xFFE5E7EB),
        ),
        borderRadius: BorderRadius.circular(12),
      ),
      padding: const EdgeInsets.all(14),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _PriorityChip(priority: record.priority),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Text(
                        _categoryLabel(record.serviceCategory),
                        style: const TextStyle(
                          fontSize: 11,
                          color: AppColors.brandGray,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                    _StatusChip(status: record.serviceStatus),
                  ],
                ),
                const SizedBox(height: 3),
                Text(
                  record.title,
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827),
                  ),
                ),
                const SizedBox(height: 4),
                Row(
                  children: [
                    if (record.completedDate != null) ...[
                      const Icon(Icons.check_circle_outline_rounded,
                          size: 12, color: AppColors.success),
                      const SizedBox(width: 4),
                      Text(
                        'Done ${DateFormat('MMM d, yyyy').format(record.completedDate!)}',
                        style: const TextStyle(
                            fontSize: 12, color: AppColors.success),
                      ),
                    ] else if (record.scheduledDate != null) ...[
                      const Icon(Icons.calendar_today_outlined,
                          size: 12, color: AppColors.brandGray),
                      const SizedBox(width: 4),
                      Text(
                        DateFormat('MMM d, yyyy').format(record.scheduledDate!),
                        style: const TextStyle(
                            fontSize: 12, color: AppColors.brandGray),
                      ),
                    ],
                    if (record.serviceProvider != null) ...[
                      const SizedBox(width: 8),
                      Flexible(
                        child: Text(
                          '· ${record.serviceProvider}',
                          style: const TextStyle(
                              fontSize: 12, color: AppColors.brandGray),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                  ],
                ),
                if (record.isWarrantyWork) ...[
                  const SizedBox(height: 3),
                  const Row(
                    children: [
                      Icon(Icons.verified_outlined,
                          size: 11, color: AppColors.primary),
                      SizedBox(width: 3),
                      Text(
                        'Warranty Work',
                        style: TextStyle(
                            fontSize: 11, color: AppColors.primary),
                      ),
                    ],
                  ),
                ],
                if (record.isOverdue) ...[
                  const SizedBox(height: 3),
                  const Row(
                    children: [
                      Icon(Icons.warning_amber_rounded,
                          size: 12, color: AppColors.warning),
                      SizedBox(width: 4),
                      Text(
                        'Overdue',
                        style: TextStyle(
                            fontSize: 11,
                            color: AppColors.warning,
                            fontWeight: FontWeight.w600),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
          PopupMenuButton<String>(
            icon: const Icon(Icons.more_vert_rounded,
                size: 20, color: AppColors.brandGray),
            onSelected: (v) {
              if (v == 'complete' && onComplete != null) onComplete!();
              if (v == 'edit') onEdit();
              if (v == 'delete') onDelete();
            },
            itemBuilder: (_) => [
              if (!record.isCompleted && onComplete != null)
                const PopupMenuItem(
                    value: 'complete', child: Text('Complete')),
              const PopupMenuItem(value: 'edit', child: Text('Edit')),
              const PopupMenuItem(
                value: 'delete',
                child: Text('Delete',
                    style: TextStyle(color: AppColors.danger)),
              ),
            ],
          ),
        ],
      ),
    );
  }

  static String categoryLabel(String category) =>
      switch (category.toLowerCase()) {
        'fluidchange' => 'Fluid Change',
        'tireservice' => 'Tire Service',
        'brakeservice' => 'Brake Service',
        'enginemaintenance' => 'Engine Maintenance',
        'transmissionservice' => 'Transmission',
        'electricalrepair' => 'Electrical Repair',
        'bodywork' => 'Body Work',
        'preventativemaintenance' => 'Preventative Maintenance',
        'unplannedrepair' => 'Unplanned Repair',
        _ => category,
      };

  String _categoryLabel(String c) => categoryLabel(c);
}

// ── Priority chip ─────────────────────────────────────────────────────────────

class ServicePriorityChip extends StatelessWidget {
  final String priority;
  const ServicePriorityChip({super.key, required this.priority});

  @override
  Widget build(BuildContext context) {
    return _PriorityChip(priority: priority);
  }
}

class _PriorityChip extends StatelessWidget {
  final String priority;
  const _PriorityChip({required this.priority});

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (priority.toLowerCase()) {
      'critical' => ('Critical', AppColors.danger),
      'urgent' => ('Urgent', const Color(0xFFEF4444)),
      'important' => ('Important', AppColors.warning),
      _ => ('Routine', AppColors.brandGray),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Text(
        label,
        style: TextStyle(
            fontSize: 10, fontWeight: FontWeight.w700, color: color),
      ),
    );
  }
}

// ── Status chip ───────────────────────────────────────────────────────────────

class ServiceStatusChip extends StatelessWidget {
  final String status;
  const ServiceStatusChip({super.key, required this.status});

  @override
  Widget build(BuildContext context) => _StatusChip(status: status);
}

class _StatusChip extends StatelessWidget {
  final String status;
  const _StatusChip({required this.status});

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status.toLowerCase()) {
      'completed' => ('Completed', AppColors.success),
      'inprogress' => ('In Progress', AppColors.primary),
      'scheduled' => ('Scheduled', const Color(0xFF6366F1)),
      'deferred' => ('Deferred', AppColors.warning),
      'cancelled' => ('Cancelled', AppColors.brandGray),
      _ => (status, AppColors.brandGray),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: color.withValues(alpha: 0.25)),
      ),
      child: Text(
        label,
        style: TextStyle(
            fontSize: 10, fontWeight: FontWeight.w600, color: color),
      ),
    );
  }
}

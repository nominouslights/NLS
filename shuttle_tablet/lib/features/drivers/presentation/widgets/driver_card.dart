import 'package:flutter/material.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/driver.dart';

class DriverCard extends StatelessWidget {
  final Driver driver;
  final VoidCallback onTap;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  const DriverCard({
    super.key,
    required this.driver,
    required this.onTap,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
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
              // Avatar
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: AppColors.primary.withValues(alpha: 0.25)),
                ),
                child: Center(
                  child: Text(
                    _initials(driver.firstName, driver.lastName),
                    style: const TextStyle(
                      color: AppColors.primary,
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 12),
              // Name / ID / warnings
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Flexible(
                          child: Text(
                            driver.fullName,
                            style: const TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                              color: Color(0xFF111827),
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        const SizedBox(width: 8),
                        _StatusBadge(status: driver.status),
                      ],
                    ),
                    const SizedBox(height: 3),
                    Text(
                      driver.employeeId,
                      style: const TextStyle(fontSize: 13, color: AppColors.brandGray),
                    ),
                    if (driver.hasExpiringDocuments) ...[
                      const SizedBox(height: 4),
                      const _ExpiryWarning(),
                    ],
                  ],
                ),
              ),
              // Actions
              PopupMenuButton<String>(
                icon: const Icon(Icons.more_vert_rounded, color: AppColors.brandGray),
                onSelected: (value) {
                  if (value == 'edit') onEdit();
                  if (value == 'delete') onDelete();
                },
                itemBuilder: (_) => const [
                  PopupMenuItem(value: 'edit', child: Text('Edit')),
                  PopupMenuItem(
                    value: 'delete',
                    child: Text('Delete', style: TextStyle(color: AppColors.danger)),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  String _initials(String first, String last) {
    final f = first.isNotEmpty ? first[0].toUpperCase() : '';
    final l = last.isNotEmpty ? last[0].toUpperCase() : '';
    return '$f$l';
  }
}

class _StatusBadge extends StatelessWidget {
  final DriverStatus status;
  const _StatusBadge({required this.status});

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      DriverStatus.available => ('Available', AppColors.success),
      DriverStatus.onTrip => ('On Trip', const Color(0xFFF59E0B)),
      DriverStatus.offDuty => ('Off Duty', AppColors.brandGray),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Text(
        label,
        style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: color),
      ),
    );
  }
}

class _ExpiryWarning extends StatelessWidget {
  const _ExpiryWarning();

  @override
  Widget build(BuildContext context) {
    return const Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.warning_amber_rounded, size: 14, color: AppColors.warning),
        SizedBox(width: 4),
        Text(
          'Document expiring soon',
          style: TextStyle(
            fontSize: 11,
            color: AppColors.warning,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    );
  }
}

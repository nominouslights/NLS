import 'package:flutter/material.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/client.dart';

class ClientCard extends StatelessWidget {
  final Client client;
  final VoidCallback onTap;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  const ClientCard({
    super.key,
    required this.client,
    required this.onTap,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    final endDate = client.activeContractEndDate ?? client.activeContract?.endDate;
    final isExpiring = client.listItemIsExpiringSoon || (client.activeContract?.isExpiringSoon ?? false);

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
                  color: _serviceTypeColor(client.serviceType).withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                    color: _serviceTypeColor(client.serviceType).withValues(alpha: 0.3),
                  ),
                ),
                child: Center(
                  child: Text(
                    _initials(client.businessName),
                    style: TextStyle(
                      color: _serviceTypeColor(client.serviceType),
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 12),
              // Name and contact
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Flexible(
                          child: Text(
                            client.businessName,
                            style: const TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                              color: Color(0xFF111827),
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        const SizedBox(width: 8),
                        _ServiceTypePill(serviceType: client.serviceType),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      client.primaryContactName,
                      style: const TextStyle(fontSize: 13, color: AppColors.brandGray),
                    ),
                    if (isExpiring && endDate != null) ...[
                      const SizedBox(height: 4),
                      _EndDateAlertChip(endDate: endDate),
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

  Color _serviceTypeColor(ServiceType type) =>
      type == ServiceType.corporate ? const Color(0xFFD97706) : AppColors.primary;

  String _initials(String name) {
    final parts = name.trim().split(' ');
    if (parts.length >= 2) return '${parts[0][0]}${parts[1][0]}'.toUpperCase();
    return name.substring(0, name.length >= 2 ? 2 : 1).toUpperCase();
  }
}

class _ServiceTypePill extends StatelessWidget {
  final ServiceType serviceType;
  const _ServiceTypePill({required this.serviceType});

  @override
  Widget build(BuildContext context) {
    final isCorporate = serviceType == ServiceType.corporate;
    final color = isCorporate ? const Color(0xFFD97706) : AppColors.primary;
    final label = isCorporate ? 'Corporate' : 'Community';

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
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

class _EndDateAlertChip extends StatelessWidget {
  final DateTime endDate;
  const _EndDateAlertChip({required this.endDate});

  @override
  Widget build(BuildContext context) {
    final days = endDate.difference(DateTime.now()).inDays;
    return Row(
      children: [
        const Icon(Icons.warning_amber_rounded, size: 14, color: AppColors.warning),
        const SizedBox(width: 4),
        Text(
          'Renews in $days days',
          style: const TextStyle(fontSize: 11, color: AppColors.warning, fontWeight: FontWeight.w600),
        ),
      ],
    );
  }
}

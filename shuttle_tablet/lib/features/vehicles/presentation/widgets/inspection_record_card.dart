import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/vehicle_inspection_record.dart';

class InspectionRecordCard extends StatelessWidget {
  final VehicleInspectionRecord record;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  const InspectionRecordCard({
    super.key,
    required this.record,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    final resultColor = _resultColor(record.inspectionResult);

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(
          color: record.isExpiringSoon
              ? const Color(0xFFFFE0A0)
              : const Color(0xFFE5E7EB),
        ),
        borderRadius: BorderRadius.circular(12),
      ),
      padding: const EdgeInsets.all(14),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: resultColor.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(
              Icons.fact_check_rounded,
              color: resultColor,
              size: 20,
            ),
          ),
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
                        typeLabel(record.inspectionType),
                        style: const TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w700,
                          color: Color(0xFF111827),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    InspectionResultBadge(result: record.inspectionResult),
                  ],
                ),
                const SizedBox(height: 4),
                Row(
                  children: [
                    const Icon(Icons.calendar_today_outlined,
                        size: 12, color: AppColors.brandGray),
                    const SizedBox(width: 4),
                    Text(
                      DateFormat('MMM d, yyyy').format(record.inspectedAt),
                      style: const TextStyle(
                          fontSize: 12, color: AppColors.brandGray),
                    ),
                    if (record.inspectionFacility != null) ...[
                      const SizedBox(width: 8),
                      Flexible(
                        child: Text(
                          '· ${record.inspectionFacility}',
                          style: const TextStyle(
                              fontSize: 12, color: AppColors.brandGray),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                  ],
                ),
                if (record.certificateNumber != null) ...[
                  const SizedBox(height: 2),
                  Text(
                    'Cert #${record.certificateNumber}',
                    style: const TextStyle(
                        fontSize: 11, color: AppColors.brandGray),
                  ),
                ],
                if (record.expiresAt != null) ...[
                  const SizedBox(height: 4),
                  Row(
                    children: [
                      if (record.isExpiringSoon) ...[
                        const Icon(Icons.warning_amber_rounded,
                            size: 12, color: AppColors.warning),
                        const SizedBox(width: 4),
                      ],
                      Text(
                        'Expires ${DateFormat('MMM d, yyyy').format(record.expiresAt!)}',
                        style: TextStyle(
                          fontSize: 11,
                          color: record.isExpiringSoon
                              ? AppColors.warning
                              : AppColors.brandGray,
                          fontWeight: record.isExpiringSoon
                              ? FontWeight.w600
                              : FontWeight.normal,
                        ),
                      ),
                    ],
                  ),
                ],
                if (!record.isPassed &&
                    record.deficienciesNotes != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    record.deficienciesNotes!,
                    style: const TextStyle(
                        fontSize: 11, color: AppColors.danger),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ],
            ),
          ),
          PopupMenuButton<String>(
            icon: const Icon(Icons.more_vert_rounded,
                size: 20, color: AppColors.brandGray),
            onSelected: (v) {
              if (v == 'edit') onEdit();
              if (v == 'delete') onDelete();
            },
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'edit', child: Text('Edit')),
              PopupMenuItem(
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

  static Color _resultColor(String result) => switch (result.toLowerCase()) {
        'pass' => AppColors.success,
        'passwithconditions' => AppColors.warning,
        'fail' => AppColors.danger,
        _ => AppColors.brandGray,
      };

  static String typeLabel(String type) => switch (type.toLowerCase()) {
        'provincialsafety' => 'Provincial Safety',
        'annualmechanical' => 'Annual Mechanical',
        'insurancesurvey' => 'Insurance Survey',
        'internalquality' => 'Internal Quality',
        'dot' => 'DOT Inspection',
        _ => type,
      };
}

class InspectionResultBadge extends StatelessWidget {
  final String result;
  const InspectionResultBadge({super.key, required this.result});

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (result.toLowerCase()) {
      'pass' => ('Pass', AppColors.success),
      'passwithconditions' => ('Pass w/ Conditions', AppColors.warning),
      'fail' => ('Fail', AppColors.danger),
      _ => (result, AppColors.brandGray),
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
        style: TextStyle(
            fontSize: 11, fontWeight: FontWeight.w600, color: color),
      ),
    );
  }
}

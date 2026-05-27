import 'package:flutter/material.dart';
import '../../../../core/theme/app_colors.dart';

class VehicleStatusBadge extends StatelessWidget {
  final String status;
  const VehicleStatusBadge({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    final (label, color) = resolve(status.toLowerCase());
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

  static (String, Color) resolve(String s) => switch (s) {
        'active' => ('Active', AppColors.success),
        'inmaintenance' => ('In Maintenance', AppColors.warning),
        'outofservice' => ('Out of Service', AppColors.danger),
        'retired' => ('Retired', AppColors.brandGray),
        _ => (s, AppColors.brandGray),
      };

  static Color colorForStatus(String status) =>
      resolve(status.toLowerCase()).$2;
}

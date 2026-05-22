import 'package:flutter/material.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/client.dart';

class ComplianceNotesPanel extends StatelessWidget {
  final Client client;
  const ComplianceNotesPanel({super.key, required this.client});

  @override
  Widget build(BuildContext context) {
    if (client.complianceNotes == null && !client.isMinesite) return const SizedBox.shrink();

    return Container(
      margin: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      decoration: BoxDecoration(
        color: client.isMinesite ? const Color(0xFFFFFBEB) : Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: client.isMinesite ? const Color(0xFFF59E0B) : const Color(0xFFE5E7EB),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 14, 16, 12),
            child: Row(
              children: [
                Icon(
                  Icons.security_outlined,
                  size: 18,
                  color: client.isMinesite ? const Color(0xFF92400E) : AppColors.brandGray,
                ),
                const SizedBox(width: 8),
                Text(
                  'Compliance',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: client.isMinesite ? const Color(0xFF92400E) : const Color(0xFF374151),
                    letterSpacing: 0.5,
                  ),
                ),
                if (client.isMinesite) ...[
                  const SizedBox(width: 8),
                  const Text(
                    '◆ Mine Site Rules Apply',
                    style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: Color(0xFF92400E)),
                  ),
                ],
              ],
            ),
          ),
          if (client.complianceNotes != null)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
              child: Text(
                client.complianceNotes!,
                style: const TextStyle(fontSize: 14, color: Color(0xFF374151), height: 1.5),
              ),
            ),
        ],
      ),
    );
  }
}

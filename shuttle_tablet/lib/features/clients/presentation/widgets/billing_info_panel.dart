import 'package:flutter/material.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/client.dart';

class BillingInfoPanel extends StatelessWidget {
  final Client client;
  const BillingInfoPanel({super.key, required this.client});

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _PanelHeader(title: 'Billing', icon: Icons.receipt_long_outlined),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
            child: Column(
              children: [
                _BillingRow(label: 'GST/HST Number', value: client.gstHstNumber ?? '—'),
                const SizedBox(height: 8),
                _BillingRow(label: 'Payment Method', value: client.preferredPaymentMethod),
                const SizedBox(height: 8),
                _BillingRow(label: 'Payment Terms', value: 'Net ${client.netPaymentTerms}'),
                const SizedBox(height: 8),
                _BillingRow(
                  label: 'Outstanding Balance',
                  value: '\$${client.outstandingBalance.toStringAsFixed(2)}',
                  valueStyle: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 14,
                    color: client.outstandingBalance > 0 ? AppColors.danger : AppColors.success,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _BillingRow extends StatelessWidget {
  final String label;
  final String value;
  final TextStyle? valueStyle;

  const _BillingRow({required this.label, required this.value, this.valueStyle});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Text(label, style: const TextStyle(fontSize: 13, color: AppColors.brandGray)),
        const Spacer(),
        Text(
          value,
          style: valueStyle ?? const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Color(0xFF111827)),
        ),
      ],
    );
  }
}

class _PanelHeader extends StatelessWidget {
  final String title;
  final IconData icon;
  const _PanelHeader({required this.title, required this.icon});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 12),
      child: Row(
        children: [
          Icon(icon, size: 18, color: AppColors.brandGray),
          const SizedBox(width: 8),
          Text(
            title,
            style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: Color(0xFF374151), letterSpacing: 0.5),
          ),
        ],
      ),
    );
  }
}

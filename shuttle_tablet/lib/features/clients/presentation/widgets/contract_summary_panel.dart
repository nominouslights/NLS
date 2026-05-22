import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/contract.dart';

class ContractSummaryPanel extends StatefulWidget {
  final Contract? contract;
  final bool isAdmin;
  final VoidCallback? onManage;

  const ContractSummaryPanel({
    super.key,
    required this.contract,
    this.isAdmin = false,
    this.onManage,
  });

  @override
  State<ContractSummaryPanel> createState() => _ContractSummaryPanelState();
}

class _ContractSummaryPanelState extends State<ContractSummaryPanel> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final contract = widget.contract;

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
          _PanelHeader(
            title: 'Contract',
            icon: Icons.description_outlined,
            trailing: widget.isAdmin && widget.onManage != null
                ? TextButton.icon(
                    onPressed: widget.onManage,
                    icon: const Icon(Icons.edit_outlined, size: 16),
                    label: const Text('Manage'),
                    style: TextButton.styleFrom(
                      foregroundColor: AppColors.primary,
                      visualDensity: VisualDensity.compact,
                    ),
                  )
                : null,
          ),
          if (contract == null)
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 0, 16, 16),
              child: Text(
                'No active contract on file.',
                style: TextStyle(color: AppColors.brandGray, fontSize: 14),
              ),
            )
          else ...[
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Column(
                children: [
                  _DateRow(label: 'Start Date', date: contract.startDate),
                  const SizedBox(height: 8),
                  _DateRow(
                    label: 'Renewal Date',
                    date: contract.renewalDate,
                    alert: contract.isExpiringSoon,
                  ),
                  if (contract.notes != null) ...[
                    const SizedBox(height: 8),
                    Align(
                      alignment: Alignment.centerLeft,
                      child: Text(
                        contract.notes!,
                        style: const TextStyle(fontSize: 13, color: AppColors.textSecondary),
                      ),
                    ),
                  ],
                  const SizedBox(height: 12),
                ],
              ),
            ),
            // Rate lines expandable section
            InkWell(
              onTap: () => setState(() => _expanded = !_expanded),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                child: Row(
                  children: [
                    const Text(
                      'Rate Lines',
                      style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Color(0xFF374151)),
                    ),
                    const SizedBox(width: 6),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
                      decoration: BoxDecoration(
                        color: AppColors.primary.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Text(
                        '${contract.rateLines.length}',
                        style: const TextStyle(fontSize: 11, color: AppColors.primary, fontWeight: FontWeight.w700),
                      ),
                    ),
                    const Spacer(),
                    Icon(
                      _expanded ? Icons.expand_less_rounded : Icons.expand_more_rounded,
                      color: AppColors.brandGray,
                      size: 20,
                    ),
                  ],
                ),
              ),
            ),
            if (_expanded && contract.rateLines.isNotEmpty) ...[
              const Divider(height: 1, indent: 16, endIndent: 16),
              _RateLinesTable(rateLines: contract.rateLines),
              const SizedBox(height: 8),
            ],
          ],
        ],
      ),
    );
  }
}

class _DateRow extends StatelessWidget {
  final String label;
  final DateTime date;
  final bool alert;

  const _DateRow({required this.label, required this.date, this.alert = false});

  @override
  Widget build(BuildContext context) {
    final fmt = DateFormat('MMM d, yyyy');
    return Row(
      children: [
        Text(label, style: const TextStyle(fontSize: 13, color: AppColors.brandGray)),
        const Spacer(),
        if (alert)
          const Padding(
            padding: EdgeInsets.only(right: 6),
            child: Icon(Icons.warning_amber_rounded, size: 14, color: AppColors.warning),
          ),
        Text(
          fmt.format(date),
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: alert ? AppColors.warning : const Color(0xFF111827),
          ),
        ),
      ],
    );
  }
}

class _RateLinesTable extends StatelessWidget {
  final List rateLines;
  const _RateLinesTable({required this.rateLines});

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: DataTable(
        headingRowHeight: 36,
        dataRowMinHeight: 36,
        dataRowMaxHeight: 48,
        horizontalMargin: 0,
        columnSpacing: 16,
        headingTextStyle: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: AppColors.brandGray),
        columns: const [
          DataColumn(label: Text('CODE')),
          DataColumn(label: Text('VEHICLE')),
          DataColumn(label: Text('MAX KM')),
          DataColumn(label: Text('CARGO')),
          DataColumn(label: Text('RATE/DAY'), numeric: true),
        ],
        rows: rateLines.map((r) {
          return DataRow(cells: [
            DataCell(Text(r.billingCode, style: const TextStyle(fontFamily: 'monospace', fontSize: 12, fontWeight: FontWeight.w600))),
            DataCell(Text(r.vehicleType, style: const TextStyle(fontSize: 13))),
            DataCell(Text(r.maxDistanceKm != null ? '${r.maxDistanceKm} km' : '—', style: const TextStyle(fontSize: 13))),
            DataCell(Icon(
              r.cargoIncluded ? Icons.check_circle_outline_rounded : Icons.remove,
              size: 16,
              color: r.cargoIncluded ? AppColors.success : AppColors.brandGray,
            )),
            DataCell(Text(
              '\$${r.dayRate.toStringAsFixed(0)}',
              style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
            )),
          ]);
        }).toList(),
      ),
    );
  }
}

class _PanelHeader extends StatelessWidget {
  final String title;
  final IconData icon;
  final Widget? trailing;
  const _PanelHeader({required this.title, required this.icon, this.trailing});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 14, 12, 12),
      child: Row(
        children: [
          Icon(icon, size: 18, color: AppColors.brandGray),
          const SizedBox(width: 8),
          Text(
            title,
            style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: Color(0xFF374151), letterSpacing: 0.5),
          ),
          const Spacer(),
          if (trailing != null) trailing!,
        ],
      ),
    );
  }
}

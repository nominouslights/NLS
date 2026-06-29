import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/invoice.dart';
import '../../domain/entities/invoice_line_item.dart';
import '../../domain/usecases/get_invoice_by_id_usecase.dart';
import '../providers/invoice_notifier.dart';

class InvoiceSummaryCard extends ConsumerStatefulWidget {
  final Invoice invoice;
  final String clientId;

  const InvoiceSummaryCard({
    super.key,
    required this.invoice,
    required this.clientId,
  });

  @override
  ConsumerState<InvoiceSummaryCard> createState() => _InvoiceSummaryCardState();
}

class _InvoiceSummaryCardState extends ConsumerState<InvoiceSummaryCard> {
  bool _expanded = false;
  Invoice? _detail;
  bool _loadingDetail = false;

  Future<void> _loadDetail() async {
    if (_detail != null || _loadingDetail) return;
    setState(() => _loadingDetail = true);
    final result =
        await sl<GetInvoiceByIdUseCase>()(widget.invoice.id);
    if (!mounted) return;
    setState(() {
      _loadingDetail = false;
      _detail = result.fold((_) => null, (inv) => inv);
    });
  }

  Future<void> _handleAction(
    BuildContext context,
    String action,
    Future<void> Function() perform,
  ) async {
    try {
      await perform();
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Failed to $action: $e'),
            backgroundColor: AppColors.danger,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final inv = widget.invoice;
    final fmt = DateFormat('MMM d, yyyy');
    final moneyFmt = NumberFormat.currency(symbol: '\$', decimalDigits: 2);

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        border: Border.all(color: const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(12),
        color: const Color(0xFFF9FAFB),
      ),
      child: Column(
        children: [
          // Header
          InkWell(
            borderRadius: const BorderRadius.vertical(top: Radius.circular(12)),
            onTap: () async {
              setState(() => _expanded = !_expanded);
              if (_expanded) await _loadDetail();
            },
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Text(
                              inv.invoiceNumber,
                              style: const TextStyle(
                                fontSize: 14,
                                fontWeight: FontWeight.w700,
                                color: Color(0xFF111827),
                              ),
                            ),
                            const SizedBox(width: 10),
                            _StatusChip(status: inv.status),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          'Issued ${fmt.format(inv.issuedDate)} · Due ${fmt.format(inv.dueDate)}',
                          style: const TextStyle(
                            fontSize: 12,
                            color: AppColors.brandGray,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          moneyFmt.format(inv.totalAmount),
                          style: const TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF111827),
                          ),
                        ),
                      ],
                    ),
                  ),
                  _ActionButtons(
                    invoice: inv,
                    clientId: widget.clientId,
                    onAction: _handleAction,
                  ),
                  Icon(
                    _expanded
                        ? Icons.expand_less_rounded
                        : Icons.expand_more_rounded,
                    color: AppColors.brandGray,
                  ),
                ],
              ),
            ),
          ),
          // Expanded line items
          if (_expanded) ...[
            const Divider(height: 1, color: Color(0xFFE5E7EB)),
            if (_loadingDetail)
              const Padding(
                padding: EdgeInsets.all(16),
                child: Center(
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              )
            else if (_detail == null)
              const Padding(
                padding: EdgeInsets.all(12),
                child: Text(
                  'Unable to load line items.',
                  style: TextStyle(fontSize: 12, color: AppColors.brandGray),
                ),
              )
            else
              _LineItemsTable(
                lineItems: _detail!.lineItems,
                totalAmount: _detail!.totalAmount,
              ),
          ],
        ],
      ),
    );
  }
}

class _StatusChip extends StatelessWidget {
  final String status;
  const _StatusChip({required this.status});

  @override
  Widget build(BuildContext context) {
    final Color bg;
    final Color fg;

    switch (status.toLowerCase()) {
      case 'paid':
        bg = AppColors.success.withValues(alpha: 0.12);
        fg = AppColors.success;
        break;
      case 'sent':
        bg = const Color(0xFFEFF6FF);
        fg = const Color(0xFF2563EB);
        break;
      case 'overdue':
        bg = AppColors.danger.withValues(alpha: 0.12);
        fg = AppColors.danger;
        break;
      case 'void':
      case 'draft':
      default:
        bg = const Color(0xFFF3F4F6);
        fg = AppColors.brandGray;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(10),
      ),
      child: Text(
        status,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: fg,
        ),
      ),
    );
  }
}

class _ActionButtons extends ConsumerWidget {
  final Invoice invoice;
  final String clientId;
  final Future<void> Function(
    BuildContext context,
    String action,
    Future<void> Function() perform,
  ) onAction;

  const _ActionButtons({
    required this.invoice,
    required this.clientId,
    required this.onAction,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final notifier = ref.read(invoiceNotifierProvider(clientId).notifier);

    final buttons = <Widget>[];

    if (invoice.status == 'Draft') {
      buttons.add(
        _SmallButton(
          label: 'Send',
          color: const Color(0xFF2563EB),
          onPressed: () => onAction(
            context,
            'send invoice',
            () => notifier.markSent(invoice.id),
          ),
        ),
      );
    }

    if (invoice.status == 'Sent' || invoice.status == 'Overdue') {
      buttons.add(
        _SmallButton(
          label: 'Mark Paid',
          color: AppColors.success,
          onPressed: () => onAction(
            context,
            'mark invoice as paid',
            () => notifier.markPaid(invoice.id),
          ),
        ),
      );
    }

    if (invoice.status != 'Void' && invoice.status != 'Paid') {
      buttons.add(
        _SmallButton(
          label: 'Void',
          color: AppColors.danger,
          onPressed: () async {
            final confirmed = await showDialog<bool>(
              context: context,
              builder: (ctx) => AlertDialog(
                title: const Text('Void Invoice'),
                content: Text(
                  'Void ${invoice.invoiceNumber}? This cannot be undone.',
                ),
                actions: [
                  TextButton(
                    onPressed: () => Navigator.pop(ctx, false),
                    child: const Text('Cancel'),
                  ),
                  FilledButton(
                    onPressed: () => Navigator.pop(ctx, true),
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.danger,
                    ),
                    child: const Text('Void'),
                  ),
                ],
              ),
            );
            if (confirmed == true && context.mounted) {
              await onAction(
                context,
                'void invoice',
                () => notifier.voidInvoice(invoice.id),
              );
            }
          },
        ),
      );
    }

    if (buttons.isEmpty) return const SizedBox.shrink();

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: buttons
          .map(
            (btn) => Padding(
              padding: const EdgeInsets.only(right: 6),
              child: btn,
            ),
          )
          .toList(),
    );
  }
}

class _SmallButton extends StatelessWidget {
  final String label;
  final Color color;
  final VoidCallback onPressed;

  const _SmallButton({
    required this.label,
    required this.color,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 30,
      child: TextButton(
        onPressed: onPressed,
        style: TextButton.styleFrom(
          foregroundColor: color,
          padding: const EdgeInsets.symmetric(horizontal: 10),
          minimumSize: Size.zero,
          tapTargetSize: MaterialTapTargetSize.shrinkWrap,
          side: BorderSide(color: color.withValues(alpha: 0.4)),
          shape:
              RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        ),
        child: Text(label, style: const TextStyle(fontSize: 12)),
      ),
    );
  }
}

class _LineItemsTable extends StatelessWidget {
  final List<InvoiceLineItem> lineItems;
  final double totalAmount;

  const _LineItemsTable({
    required this.lineItems,
    required this.totalAmount,
  });

  @override
  Widget build(BuildContext context) {
    final moneyFmt = NumberFormat.currency(symbol: '\$', decimalDigits: 2);

    return Padding(
      padding: const EdgeInsets.fromLTRB(14, 10, 14, 14),
      child: Column(
        children: [
          Row(
            children: const [
              Expanded(flex: 3, child: _TableHeader('Description')),
              Expanded(flex: 2, child: _TableHeader('Rate')),
              Expanded(flex: 1, child: _TableHeader('Qty')),
              Expanded(flex: 2, child: _TableHeader('Total')),
            ],
          ),
          const Divider(height: 12, color: Color(0xFFE5E7EB)),
          ...lineItems.map(
            (item) => Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                children: [
                  Expanded(
                    flex: 3,
                    child: Text(
                      item.description,
                      style: const TextStyle(fontSize: 12),
                    ),
                  ),
                  Expanded(
                    flex: 2,
                    child: Text(
                      moneyFmt.format(item.unitRate),
                      style: const TextStyle(fontSize: 12),
                    ),
                  ),
                  Expanded(
                    flex: 1,
                    child: Text(
                      item.quantity.toStringAsFixed(
                        item.quantity == item.quantity.roundToDouble() ? 0 : 2,
                      ),
                      style: const TextStyle(fontSize: 12),
                    ),
                  ),
                  Expanded(
                    flex: 2,
                    child: Text(
                      moneyFmt.format(item.lineTotal),
                      style: const TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const Divider(height: 12, color: Color(0xFFE5E7EB)),
          Align(
            alignment: Alignment.centerRight,
            child: Text(
              'Total: ${moneyFmt.format(totalAmount)}',
              style: const TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _TableHeader extends StatelessWidget {
  final String label;
  const _TableHeader(this.label);

  @override
  Widget build(BuildContext context) => Text(
        label.toUpperCase(),
        style: const TextStyle(
          fontSize: 10,
          fontWeight: FontWeight.w700,
          color: AppColors.brandGray,
          letterSpacing: 0.6,
        ),
      );
}

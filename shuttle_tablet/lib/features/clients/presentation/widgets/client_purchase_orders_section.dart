import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/contract.dart';
import '../../domain/entities/purchase_order.dart';
import '../../domain/repositories/i_purchase_order_repository.dart';
import '../../domain/usecases/purchase_order_usecases.dart';
import '../providers/contracts_provider.dart';
import '../providers/purchase_orders_provider.dart';

class ClientPurchaseOrdersSection extends ConsumerStatefulWidget {
  final String clientId;
  const ClientPurchaseOrdersSection({super.key, required this.clientId});

  @override
  ConsumerState<ClientPurchaseOrdersSection> createState() =>
      _ClientPurchaseOrdersSectionState();
}

class _ClientPurchaseOrdersSectionState extends ConsumerState<ClientPurchaseOrdersSection> {
  Future<void> _showUpsertDialog({PurchaseOrder? existing}) async {
    final isEdit = existing != null;
    PurchaseOrder? detail = existing;

    if (isEdit) {
      final result = await sl<GetPurchaseOrderByIdUseCase>()(
        GetPurchaseOrderByIdParams(
          clientId: widget.clientId,
          purchaseOrderId: existing.id,
        ),
      );
      detail = result.fold(
        (failure) {
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text(failure.message), backgroundColor: AppColors.danger),
            );
          }
          return null;
        },
        (po) => po,
      );
      if (detail == null || !mounted) return;
    }

    final poNumberCtrl = TextEditingController(text: detail?.poNumber ?? '');
    final detailsCtrl = TextEditingController(text: detail?.details ?? '');
    DateTime startDate = detail?.startDate ?? DateTime.now();
    final lineRows = <_LineItemRow>[];

    if (detail != null && detail.lineItems.isNotEmpty) {
      for (final item in detail.lineItems) {
        lineRows.add(_LineItemRow.fromItem(item));
      }
    } else {
      lineRows.add(_LineItemRow.empty());
    }

    final contracts = ref.read(contractsProvider(widget.clientId)).value ?? [];
    final selectedContractIds = {...?detail?.linkedContractIds};

    if (!mounted) return;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) {
          final poTotal = lineRows.fold<double>(
            0,
            (sum, row) => sum + row.lineTotal,
          );

          return AlertDialog(
            title: Text(
              isEdit ? 'Edit Purchase Order' : 'Add Purchase Order',
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
            content: SizedBox(
              width: 560,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    TextField(
                      controller: poNumberCtrl,
                      decoration: InputDecoration(
                        labelText: 'PO Number *',
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                        isDense: true,
                      ),
                    ),
                    const SizedBox(height: 12),
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      title: const Text('PO Start Date *',
                          style: TextStyle(fontSize: 13, color: AppColors.brandGray)),
                      subtitle: Text(DateFormat('MMM d, yyyy').format(startDate),
                          style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14)),
                      trailing: const Icon(Icons.calendar_today_outlined, size: 18),
                      onTap: () async {
                        final picked = await showDatePicker(
                          context: ctx,
                          initialDate: startDate,
                          firstDate: DateTime(2020),
                          lastDate: DateTime(2040),
                        );
                        if (picked != null) setDialogState(() => startDate = picked);
                      },
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: detailsCtrl,
                      maxLines: 3,
                      decoration: InputDecoration(
                        labelText: 'Details (optional)',
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                        isDense: true,
                      ),
                    ),
                    if (contracts.isNotEmpty) ...[
                      const SizedBox(height: 16),
                      const Text('Linked Contracts (optional)',
                          style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600)),
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: contracts.map((contract) {
                          final selected = selectedContractIds.contains(contract.id);
                          return FilterChip(
                            label: Text(_contractChipLabel(contract)),
                            selected: selected,
                            onSelected: (value) {
                              setDialogState(() {
                                if (value) {
                                  selectedContractIds.add(contract.id);
                                } else {
                                  selectedContractIds.remove(contract.id);
                                }
                              });
                            },
                          );
                        }).toList(),
                      ),
                    ],
                    const SizedBox(height: 16),
                    Row(
                      children: [
                        const Expanded(
                          child: Text('Line Items *',
                              style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600)),
                        ),
                        TextButton.icon(
                          onPressed: () {
                            setDialogState(() => lineRows.add(_LineItemRow.empty()));
                          },
                          icon: const Icon(Icons.add_rounded, size: 16),
                          label: const Text('Add Row'),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    ...lineRows.asMap().entries.map((entry) {
                      final index = entry.key;
                      final row = entry.value;
                      return Padding(
                        padding: const EdgeInsets.only(bottom: 10),
                        child: _LineItemEditor(
                          row: row,
                          onChanged: () => setDialogState(() {}),
                          onRemove: lineRows.length > 1
                              ? () => setDialogState(() => lineRows.removeAt(index))
                              : null,
                        ),
                      );
                    }),
                    Align(
                      alignment: Alignment.centerRight,
                      child: Text(
                        'PO Total: ${_formatMoney(poTotal)}',
                        style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
                      ),
                    ),
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
              FilledButton(
                onPressed: () => Navigator.pop(ctx, true),
                style: FilledButton.styleFrom(backgroundColor: AppColors.primary),
                child: Text(isEdit ? 'Save' : 'Add PO'),
              ),
            ],
          );
        },
      ),
    );

    if (confirmed != true || !mounted) {
      poNumberCtrl.dispose();
      detailsCtrl.dispose();
      for (final row in lineRows) {
        row.dispose();
      }
      return;
    }

    final lineItems = <PurchaseOrderLineItemParams>[];
    for (final row in lineRows) {
      final description = row.descriptionCtrl.text.trim();
      final unitRate = double.tryParse(row.unitRateCtrl.text.trim()) ?? -1;
      final quantity = double.tryParse(row.quantityCtrl.text.trim()) ?? 0;
      if (description.isEmpty || unitRate < 0 || quantity <= 0) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Each line item needs description, unit rate >= 0, and quantity > 0.'),
            backgroundColor: AppColors.danger,
          ),
        );
        poNumberCtrl.dispose();
        detailsCtrl.dispose();
        for (final row in lineRows) {
          row.dispose();
        }
        return;
      }
      lineItems.add(PurchaseOrderLineItemParams(
        description: description,
        unitRate: unitRate,
        quantity: quantity,
      ));
    }

    final poNumber = poNumberCtrl.text.trim();
    if (poNumber.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('PO number is required.'), backgroundColor: AppColors.danger),
      );
      poNumberCtrl.dispose();
      detailsCtrl.dispose();
      for (final row in lineRows) {
        row.dispose();
      }
      return;
    }

    try {
      if (isEdit) {
        await ref.read(purchaseOrdersProvider(widget.clientId).notifier).updatePurchaseOrder(
              existing.id,
              UpdatePurchaseOrderParams(
                clientId: widget.clientId,
                poNumber: poNumber,
                startDate: startDate,
                details: detailsCtrl.text.trim().isEmpty ? null : detailsCtrl.text.trim(),
                lineItems: lineItems,
                contractIds: selectedContractIds.toList(),
              ),
            );
      } else {
        await ref.read(purchaseOrdersProvider(widget.clientId).notifier).createPurchaseOrder(
              CreatePurchaseOrderParams(
                clientId: widget.clientId,
                poNumber: poNumber,
                startDate: startDate,
                details: detailsCtrl.text.trim().isEmpty ? null : detailsCtrl.text.trim(),
                lineItems: lineItems,
                contractIds: selectedContractIds.toList(),
              ),
            );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to save PO: $e'), backgroundColor: AppColors.danger),
        );
      }
    }

    poNumberCtrl.dispose();
    detailsCtrl.dispose();
    for (final row in lineRows) {
      row.dispose();
    }
  }

  @override
  Widget build(BuildContext context) {
    final purchaseOrdersAsync = ref.watch(purchaseOrdersProvider(widget.clientId));
    final contracts = ref.watch(contractsProvider(widget.clientId)).value ?? [];

    return _PoSectionCard(
      title: 'Purchase Orders',
      trailing: TextButton.icon(
        onPressed: () => _showUpsertDialog(),
        icon: const Icon(Icons.add_rounded, size: 16),
        label: const Text('Add PO'),
        style: TextButton.styleFrom(foregroundColor: AppColors.primary, padding: EdgeInsets.zero),
      ),
      child: purchaseOrdersAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Text('Error loading purchase orders: $e',
            style: const TextStyle(color: AppColors.danger, fontSize: 13)),
        data: (purchaseOrders) {
          if (purchaseOrders.isEmpty) {
            return const Center(
              child: Text('No purchase orders on file.',
                  style: TextStyle(color: AppColors.brandGray, fontSize: 13)),
            );
          }
          return Column(
            children: purchaseOrders
                .map((po) => _PurchaseOrderCard(
                      purchaseOrder: po,
                      contracts: contracts,
                      onEdit: () => _showUpsertDialog(existing: po),
                    ))
                .toList(),
          );
        },
      ),
    );
  }
}

class _PurchaseOrderCard extends ConsumerStatefulWidget {
  final PurchaseOrder purchaseOrder;
  final List<Contract> contracts;
  final VoidCallback onEdit;

  const _PurchaseOrderCard({
    required this.purchaseOrder,
    required this.contracts,
    required this.onEdit,
  });

  @override
  ConsumerState<_PurchaseOrderCard> createState() => _PurchaseOrderCardState();
}

class _PurchaseOrderCardState extends ConsumerState<_PurchaseOrderCard> {
  bool _expanded = false;
  PurchaseOrder? _detail;
  bool _loadingDetail = false;

  Future<void> _loadDetail() async {
    if (_detail != null || _loadingDetail) return;
    setState(() => _loadingDetail = true);
    final result = await sl<GetPurchaseOrderByIdUseCase>()(
      GetPurchaseOrderByIdParams(
        clientId: widget.purchaseOrder.clientId,
        purchaseOrderId: widget.purchaseOrder.id,
      ),
    );
    if (!mounted) return;
    setState(() {
      _loadingDetail = false;
      _detail = result.fold((_) => null, (po) => po);
    });
  }

  @override
  Widget build(BuildContext context) {
    final po = widget.purchaseOrder;
    final fmt = DateFormat('MMM d, yyyy');
    final linkedLabels = widget.contracts
        .where((c) => po.linkedContractIds.contains(c.id))
        .map(_contractChipLabel)
        .toList();

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        border: Border.all(color: const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(12),
        color: const Color(0xFFF9FAFB),
      ),
      child: Column(
        children: [
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
                        Text(po.poNumber,
                            style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
                        const SizedBox(height: 4),
                        Text(
                          '${fmt.format(po.startDate)} · ${_formatMoney(po.totalValue)} · ${po.lineItemCount} line item${po.lineItemCount == 1 ? '' : 's'}',
                          style: const TextStyle(fontSize: 12, color: AppColors.brandGray),
                        ),
                        if (po.details?.isNotEmpty ?? false)
                          Padding(
                            padding: const EdgeInsets.only(top: 4),
                            child: Text(
                              po.details!,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(fontSize: 12, color: AppColors.textSecondary),
                            ),
                          ),
                        if (linkedLabels.isNotEmpty) ...[
                          const SizedBox(height: 8),
                          Wrap(
                            spacing: 6,
                            runSpacing: 6,
                            children: linkedLabels
                                .map((label) => Chip(
                                      label: Text(label, style: const TextStyle(fontSize: 11)),
                                      visualDensity: VisualDensity.compact,
                                      materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                                    ))
                                .toList(),
                          ),
                        ],
                      ],
                    ),
                  ),
                  IconButton(
                    onPressed: widget.onEdit,
                    icon: const Icon(Icons.edit_outlined, size: 18),
                    tooltip: 'Edit PO',
                  ),
                  Icon(
                    _expanded ? Icons.expand_less_rounded : Icons.expand_more_rounded,
                    color: AppColors.brandGray,
                  ),
                ],
              ),
            ),
          ),
          if (_expanded) ...[
            const Divider(height: 1, color: Color(0xFFE5E7EB)),
            if (_loadingDetail)
              const Padding(
                padding: EdgeInsets.all(16),
                child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
              )
            else if (_detail == null)
              const Padding(
                padding: EdgeInsets.all(12),
                child: Text('Unable to load line items.',
                    style: TextStyle(fontSize: 12, color: AppColors.brandGray)),
              )
            else
              _LineItemsTable(lineItems: _detail!.lineItems, totalValue: _detail!.totalValue),
          ],
        ],
      ),
    );
  }
}

class _LineItemsTable extends StatelessWidget {
  final List lineItems;
  final double totalValue;
  const _LineItemsTable({required this.lineItems, required this.totalValue});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(14, 10, 14, 14),
      child: Column(
        children: [
          Row(
            children: const [
              Expanded(flex: 3, child: _TableHeader('Description')),
              Expanded(flex: 2, child: _TableHeader('Unit Rate')),
              Expanded(flex: 2, child: _TableHeader('Qty')),
              Expanded(flex: 2, child: _TableHeader('Line Total')),
            ],
          ),
          const Divider(height: 12, color: Color(0xFFE5E7EB)),
          ...lineItems.map((item) => Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Row(
                  children: [
                    Expanded(flex: 3, child: Text(item.description, style: const TextStyle(fontSize: 12))),
                    Expanded(
                        flex: 2,
                        child: Text(_formatMoney(item.unitRate),
                            style: const TextStyle(fontSize: 12))),
                    Expanded(
                        flex: 2,
                        child: Text(item.quantity.toString(),
                            style: const TextStyle(fontSize: 12))),
                    Expanded(
                        flex: 2,
                        child: Text(_formatMoney(item.lineTotal),
                            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600))),
                  ],
                ),
              )),
          const Divider(height: 12, color: Color(0xFFE5E7EB)),
          Align(
            alignment: Alignment.centerRight,
            child: Text('Total: ${_formatMoney(totalValue)}',
                style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
          ),
        ],
      ),
    );
  }
}

class _LineItemEditor extends StatelessWidget {
  final _LineItemRow row;
  final VoidCallback onChanged;
  final VoidCallback? onRemove;

  const _LineItemEditor({
    required this.row,
    required this.onChanged,
    this.onRemove,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        border: Border.all(color: const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Column(
        children: [
          TextField(
            controller: row.descriptionCtrl,
            onChanged: (_) => onChanged(),
            decoration: InputDecoration(
              labelText: 'Description *',
              hintText: 'e.g. Passenger Transport',
              border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
              isDense: true,
            ),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: row.unitRateCtrl,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'^\d*\.?\d*'))],
                  onChanged: (_) => onChanged(),
                  decoration: InputDecoration(
                    labelText: 'Unit Rate (\$) *',
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
                    isDense: true,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: TextField(
                  controller: row.quantityCtrl,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'^\d*\.?\d*'))],
                  onChanged: (_) => onChanged(),
                  decoration: InputDecoration(
                    labelText: 'Quantity *',
                    hintText: 'e.g. 16 trips',
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
                    isDense: true,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              SizedBox(
                width: 110,
                child: InputDecorator(
                  decoration: InputDecoration(
                    labelText: 'Line Total',
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
                    isDense: true,
                  ),
                  child: Text(
                    _formatMoney(row.lineTotal),
                    style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
                  ),
                ),
              ),
              if (onRemove != null) ...[
                const SizedBox(width: 4),
                IconButton(
                  onPressed: onRemove,
                  icon: const Icon(Icons.remove_circle_outline, color: AppColors.danger),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

class _LineItemRow {
  final TextEditingController descriptionCtrl;
  final TextEditingController unitRateCtrl;
  final TextEditingController quantityCtrl;

  _LineItemRow({
    required this.descriptionCtrl,
    required this.unitRateCtrl,
    required this.quantityCtrl,
  });

  factory _LineItemRow.empty() => _LineItemRow(
        descriptionCtrl: TextEditingController(),
        unitRateCtrl: TextEditingController(),
        quantityCtrl: TextEditingController(text: '1'),
      );

  factory _LineItemRow.fromItem(dynamic item) => _LineItemRow(
        descriptionCtrl: TextEditingController(text: item.description as String),
        unitRateCtrl: TextEditingController(text: item.unitRate.toString()),
        quantityCtrl: TextEditingController(text: item.quantity.toString()),
      );

  double get lineTotal {
    final unitRate = double.tryParse(unitRateCtrl.text.trim()) ?? 0;
    final quantity = double.tryParse(quantityCtrl.text.trim()) ?? 0;
    return unitRate * quantity;
  }

  void dispose() {
    descriptionCtrl.dispose();
    unitRateCtrl.dispose();
    quantityCtrl.dispose();
  }
}

class _PoSectionCard extends StatelessWidget {
  final String title;
  final Widget child;
  final Widget? trailing;

  const _PoSectionCard({required this.title, required this.child, this.trailing});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(16),
        boxShadow: const [
          BoxShadow(color: Color(0x06000000), blurRadius: 12, offset: Offset(0, 4)),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 14, 12, 10),
            child: Row(
              children: [
                Expanded(
                  child: Text(title,
                      style: const TextStyle(
                          fontSize: 14, fontWeight: FontWeight.w700, color: Color(0xFF111827))),
                ),
                if (trailing != null) trailing!,
              ],
            ),
          ),
          const Divider(height: 1, color: Color(0xFFF3F4F6)),
          Padding(padding: const EdgeInsets.all(16), child: child),
        ],
      ),
    );
  }
}

class _TableHeader extends StatelessWidget {
  final String label;
  const _TableHeader(this.label);

  @override
  Widget build(BuildContext context) {
    return Text(
      label.toUpperCase(),
      style: const TextStyle(
          fontSize: 10, fontWeight: FontWeight.w700, color: AppColors.brandGray, letterSpacing: 0.6),
    );
  }
}

String _contractChipLabel(Contract contract) {
  final fmt = DateFormat('MMM yyyy');
  return '${fmt.format(contract.startDate)} – ${fmt.format(contract.endDate)}';
}

String _formatMoney(double value) {
  final formatter = NumberFormat.currency(symbol: '\$', decimalDigits: 2);
  return formatter.format(value);
}

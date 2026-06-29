import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../pages/create_invoice_page.dart';
import '../providers/invoice_notifier.dart';
import 'invoice_summary_card.dart';

class ClientInvoicesSection extends ConsumerWidget {
  final String clientId;
  const ClientInvoicesSection({super.key, required this.clientId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final invoicesAsync = ref.watch(invoiceNotifierProvider(clientId));

    return _InvoicesSectionCard(
      title: 'Invoices',
      trailing: TextButton.icon(
        onPressed: () => _openCreateInvoice(context),
        icon: const Icon(Icons.add_rounded, size: 16),
        label: const Text('New Invoice'),
        style: TextButton.styleFrom(
          foregroundColor: AppColors.primary,
          padding: EdgeInsets.zero,
        ),
      ),
      child: invoicesAsync.when(
        loading: () => const Padding(
          padding: EdgeInsets.all(16),
          child: Center(child: CircularProgressIndicator()),
        ),
        error: (e, _) => Padding(
          padding: const EdgeInsets.all(8),
          child: Text(
            'Error loading invoices: $e',
            style: const TextStyle(color: AppColors.danger, fontSize: 13),
          ),
        ),
        data: (invoices) {
          if (invoices.isEmpty) {
            return const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Center(
                child: Text(
                  'No invoices on file.',
                  style: TextStyle(color: AppColors.brandGray, fontSize: 13),
                ),
              ),
            );
          }
          return Column(
            children: invoices
                .map(
                  (invoice) => InvoiceSummaryCard(
                    invoice: invoice,
                    clientId: clientId,
                  ),
                )
                .toList(),
          );
        },
      ),
    );
  }

  Future<void> _openCreateInvoice(BuildContext context) async {
    await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => CreateInvoicePage(clientId: clientId),
        fullscreenDialog: true,
      ),
    );
  }
}

class _InvoicesSectionCard extends StatelessWidget {
  final String title;
  final Widget child;
  final Widget? trailing;

  const _InvoicesSectionCard({
    required this.title,
    required this.child,
    this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(16),
        boxShadow: const [
          BoxShadow(
            color: Color(0x06000000),
            blurRadius: 12,
            offset: Offset(0, 4),
          ),
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
                  child: Text(
                    title,
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF111827),
                    ),
                  ),
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

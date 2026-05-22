import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/client.dart';
import '../providers/clients_provider.dart';
import '../widgets/client_card.dart';
import 'client_detail_page.dart';
import 'client_form_sheet.dart';

class ClientsListPage extends ConsumerStatefulWidget {
  const ClientsListPage({super.key});

  @override
  ConsumerState<ClientsListPage> createState() => _ClientsListPageState();
}

class _ClientsListPageState extends ConsumerState<ClientsListPage> {
  String _search = '';
  bool? _activeFilter; // null = all, true = active, false = inactive

  @override
  Widget build(BuildContext context) {
    final clientsAsync = ref.watch(clientsProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Column(
        children: [
          // Search + filter bar
          Container(
            color: Colors.white,
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    decoration: InputDecoration(
                      hintText: 'Search clients…',
                      prefixIcon: const Icon(Icons.search_rounded, size: 20),
                      isDense: true,
                      filled: true,
                      fillColor: const Color(0xFFF9FAFB),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
                      ),
                      enabledBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
                      ),
                      contentPadding: const EdgeInsets.symmetric(vertical: 10),
                    ),
                    onChanged: (v) => setState(() => _search = v),
                  ),
                ),
                const SizedBox(width: 10),
                _FilterToggle(
                  selected: _activeFilter,
                  onChanged: (v) => setState(() => _activeFilter = v),
                ),
              ],
            ),
          ),
          const Divider(height: 1),
          // List
          Expanded(
            child: clientsAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.error_outline_rounded, size: 48, color: AppColors.danger),
                    const SizedBox(height: 12),
                    Text('$e', style: const TextStyle(color: AppColors.danger)),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: () => ref.invalidate(clientsProvider),
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
              data: (clients) {
                final filtered = _filter(clients);
                if (filtered.isEmpty) {
                  return const Center(
                    child: Text('No clients found', style: TextStyle(color: AppColors.brandGray)),
                  );
                }
                return RefreshIndicator(
                  onRefresh: () => ref.refresh(clientsProvider.future),
                  child: ListView.builder(
                    padding: const EdgeInsets.only(top: 8, bottom: 100),
                    itemCount: filtered.length,
                    itemBuilder: (_, i) {
                      final client = filtered[i];
                      return ClientCard(
                        client: client,
                        onTap: () => _openDetail(context, client.id),
                        onEdit: () => _openForm(context, client),
                        onDelete: () => _confirmDelete(context, ref, client),
                      );
                    },
                  ),
                );
              },
            ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _openForm(context, null),
        backgroundColor: AppColors.primary,
        icon: const Icon(Icons.add_rounded),
        label: const Text('Add Client'),
      ),
    );
  }

  List<Client> _filter(List<Client> all) {
    var list = all;
    if (_activeFilter != null) {
      list = list.where((c) => c.isActive == _activeFilter).toList();
    }
    if (_search.isNotEmpty) {
      final q = _search.toLowerCase();
      list = list.where((c) =>
          c.businessName.toLowerCase().contains(q) ||
          c.primaryContactName.toLowerCase().contains(q) ||
          c.email.toLowerCase().contains(q)).toList();
    }
    return list;
  }

  void _openDetail(BuildContext context, String id) {
    Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => ClientDetailPage(clientId: id),
    ));
  }

  Future<void> _openForm(BuildContext context, Client? client) async {
    final result = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => ClientFormSheet(client: client),
    );
    if (result == true) ref.invalidate(clientsProvider);
  }

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref, Client client) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Client'),
        content: Text('Are you sure you want to delete "${client.businessName}"? This cannot be undone.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      try {
        await ref.read(clientsProvider.notifier).deleteClient(client.id);
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('"${client.businessName}" deleted')),
          );
        }
      } catch (e) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Failed to delete: $e'), backgroundColor: AppColors.danger),
          );
        }
      }
    }
  }
}

class _FilterToggle extends StatelessWidget {
  final bool? selected;
  final ValueChanged<bool?> onChanged;

  const _FilterToggle({required this.selected, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      padding: const EdgeInsets.all(2),
      child: Row(
        children: [
          _Pill(label: 'All', selected: selected == null, onTap: () => onChanged(null)),
          _Pill(label: 'Active', selected: selected == true, onTap: () => onChanged(true)),
          _Pill(label: 'Inactive', selected: selected == false, onTap: () => onChanged(false)),
        ],
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;
  const _Pill({required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: selected ? Colors.white : Colors.transparent,
          borderRadius: BorderRadius.circular(8),
          boxShadow: selected ? [const BoxShadow(color: Color(0x14000000), blurRadius: 4)] : null,
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: selected ? FontWeight.w600 : FontWeight.w400,
            color: selected ? AppColors.primary : AppColors.brandGray,
          ),
        ),
      ),
    );
  }
}

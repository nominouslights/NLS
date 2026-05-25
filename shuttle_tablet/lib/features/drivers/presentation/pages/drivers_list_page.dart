import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/driver.dart';
import '../providers/drivers_provider.dart';
import '../widgets/driver_card.dart';
import 'driver_detail_page.dart';
import 'driver_form_sheet.dart';

class DriversListPage extends ConsumerStatefulWidget {
  const DriversListPage({super.key});

  @override
  ConsumerState<DriversListPage> createState() => _DriversListPageState();
}

class _DriversListPageState extends ConsumerState<DriversListPage> {
  String _search = '';
  DriverStatus? _statusFilter; // null = all

  @override
  Widget build(BuildContext context) {
    final driversAsync = ref.watch(driversProvider);

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
                      hintText: 'Search drivers…',
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
                _StatusFilter(
                  selected: _statusFilter,
                  onChanged: (v) => setState(() => _statusFilter = v),
                ),
              ],
            ),
          ),
          const Divider(height: 1),
          // List
          Expanded(
            child: driversAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.error_outline_rounded,
                        size: 48, color: AppColors.danger),
                    const SizedBox(height: 12),
                    Text('$e',
                        style: const TextStyle(color: AppColors.danger)),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: () => ref.invalidate(driversProvider),
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
              data: (drivers) {
                final filtered = _filter(drivers);
                if (filtered.isEmpty) {
                  return const Center(
                    child: Text('No drivers found',
                        style: TextStyle(color: AppColors.brandGray)),
                  );
                }
                return RefreshIndicator(
                  onRefresh: () => ref.refresh(driversProvider.future),
                  child: ListView.builder(
                    padding: const EdgeInsets.only(top: 8, bottom: 100),
                    itemCount: filtered.length,
                    itemBuilder: (_, i) {
                      final driver = filtered[i];
                      return DriverCard(
                        driver: driver,
                        onTap: () => _openDetail(context, driver.id),
                        onEdit: () => _openForm(context, driver),
                        onDelete: () => _confirmDelete(context, ref, driver),
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
        label: const Text('Add Driver'),
      ),
    );
  }

  List<Driver> _filter(List<Driver> all) {
    var list = all;
    if (_statusFilter != null) {
      list = list.where((d) => d.status == _statusFilter).toList();
    }
    if (_search.isNotEmpty) {
      final q = _search.toLowerCase();
      list = list
          .where((d) =>
              d.fullName.toLowerCase().contains(q) ||
              d.employeeId.toLowerCase().contains(q) ||
              d.email.toLowerCase().contains(q) ||
              d.phone.contains(q))
          .toList();
    }
    return list;
  }

  void _openDetail(BuildContext context, String id) {
    Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => DriverDetailPage(driverId: id),
    ));
  }

  Future<void> _openForm(BuildContext context, Driver? driver) async {
    final result = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => DriverFormSheet(driver: driver),
    );
    if (result == true) ref.invalidate(driversProvider);
  }

  Future<void> _confirmDelete(
      BuildContext context, WidgetRef ref, Driver driver) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Driver'),
        content: Text(
            'Are you sure you want to delete "${driver.fullName}"? This cannot be undone.'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style:
                FilledButton.styleFrom(backgroundColor: AppColors.danger),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      try {
        await ref.read(driversProvider.notifier).deleteDriver(driver.id);
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('"${driver.fullName}" deleted')),
          );
        }
      } catch (e) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text('Failed to delete: $e'),
                backgroundColor: AppColors.danger),
          );
        }
      }
    }
  }
}

// ── Status filter pills ───────────────────────────────────────────────────────

class _StatusFilter extends StatelessWidget {
  final DriverStatus? selected;
  final ValueChanged<DriverStatus?> onChanged;

  const _StatusFilter({required this.selected, required this.onChanged});

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
          _Pill(
              label: 'All',
              selected: selected == null,
              onTap: () => onChanged(null)),
          _Pill(
              label: 'Available',
              selected: selected == DriverStatus.available,
              onTap: () => onChanged(DriverStatus.available)),
          _Pill(
              label: 'On Trip',
              selected: selected == DriverStatus.onTrip,
              onTap: () => onChanged(DriverStatus.onTrip)),
          _Pill(
              label: 'Off Duty',
              selected: selected == DriverStatus.offDuty,
              onTap: () => onChanged(DriverStatus.offDuty)),
        ],
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;
  const _Pill(
      {required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        decoration: BoxDecoration(
          color: selected ? Colors.white : Colors.transparent,
          borderRadius: BorderRadius.circular(8),
          boxShadow: selected
              ? [const BoxShadow(color: Color(0x14000000), blurRadius: 4)]
              : null,
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: selected ? FontWeight.w600 : FontWeight.w400,
            color: selected ? AppColors.primary : AppColors.brandGray,
          ),
        ),
      ),
    );
  }
}

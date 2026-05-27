import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/vehicle.dart';
import '../providers/vehicles_provider.dart';
import '../widgets/vehicle_card.dart';
import 'vehicle_detail_page.dart';
import 'vehicle_form_sheet.dart';
import 'vehicle_out_of_service_sheet.dart';

class VehiclesListPage extends ConsumerStatefulWidget {
  const VehiclesListPage({super.key});

  @override
  ConsumerState<VehiclesListPage> createState() =>
      _VehiclesListPageState();
}

class _VehiclesListPageState extends ConsumerState<VehiclesListPage> {
  String _search = '';
  String? _statusFilter; // null = all

  @override
  Widget build(BuildContext context) {
    final vehiclesAsync = ref.watch(vehiclesProvider);

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
                      hintText: 'Search by unit code, plate, make…',
                      prefixIcon:
                          const Icon(Icons.search_rounded, size: 20),
                      isDense: true,
                      filled: true,
                      fillColor: const Color(0xFFF9FAFB),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide:
                            const BorderSide(color: Color(0xFFE5E7EB)),
                      ),
                      enabledBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide:
                            const BorderSide(color: Color(0xFFE5E7EB)),
                      ),
                      contentPadding:
                          const EdgeInsets.symmetric(vertical: 10),
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
            child: vehiclesAsync.when(
              loading: () =>
                  const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.error_outline_rounded,
                        size: 48, color: AppColors.danger),
                    const SizedBox(height: 12),
                    Text('$e',
                        style:
                            const TextStyle(color: AppColors.danger)),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: () => ref.invalidate(vehiclesProvider),
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
              data: (vehicles) {
                final filtered = _filter(vehicles);
                if (filtered.isEmpty) {
                  return const Center(
                    child: Text('No vehicles found',
                        style:
                            TextStyle(color: AppColors.brandGray)),
                  );
                }
                return RefreshIndicator(
                  onRefresh: () =>
                      ref.read(vehiclesProvider.notifier).refresh(),
                  child: ListView.builder(
                    padding:
                        const EdgeInsets.only(top: 8, bottom: 100),
                    itemCount: filtered.length,
                    itemBuilder: (_, i) {
                      final v = filtered[i];
                      return VehicleCard(
                        vehicle: v,
                        onTap: () => _openDetail(context, v.id),
                        onEdit: () => _openForm(context, v),
                        onSetOutOfService: () =>
                            _openOutOfService(context, v),
                        onChangeStatus: () =>
                            _openChangeStatus(context, v),
                        onDelete: () =>
                            _confirmDelete(context, ref, v),
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
        label: const Text('Add Vehicle'),
      ),
    );
  }

  List<Vehicle> _filter(List<Vehicle> all) {
    var list = all;
    if (_statusFilter != null) {
      list = list
          .where((v) => v.status.toLowerCase() == _statusFilter)
          .toList();
    }
    if (_search.isNotEmpty) {
      final q = _search.toLowerCase();
      list = list
          .where((v) =>
              v.unitCode.toLowerCase().contains(q) ||
              v.licensePlate.toLowerCase().contains(q) ||
              v.make.toLowerCase().contains(q) ||
              v.model.toLowerCase().contains(q) ||
              v.displayName.toLowerCase().contains(q))
          .toList();
    }
    return list;
  }

  void _openDetail(BuildContext context, String id) {
    Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => VehicleDetailPage(vehicleId: id),
    ));
  }

  Future<void> _openForm(BuildContext context, Vehicle? vehicle) async {
    final result = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => VehicleFormSheet(vehicle: vehicle),
    );
    if (result == true && mounted) ref.invalidate(vehiclesProvider);
  }

  Future<void> _openOutOfService(
      BuildContext context, Vehicle vehicle) async {
    await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => VehicleOutOfServiceSheet(vehicle: vehicle),
    );
  }

  Future<void> _openChangeStatus(
      BuildContext context, Vehicle vehicle) async {
    await showDialog<void>(
      context: context,
      builder: (ctx) => _ChangeStatusDialog(
        vehicle: vehicle,
        onSetStatus: (status) async {
          Navigator.pop(ctx);
          try {
            await ref
                .read(vehiclesProvider.notifier)
                .setStatus(vehicle.id, status);
          } catch (e) {
            if (context.mounted) {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                    content: Text('Failed: $e'),
                    backgroundColor: AppColors.danger),
              );
            }
          }
        },
      ),
    );
  }

  Future<void> _confirmDelete(
      BuildContext context, WidgetRef ref, Vehicle vehicle) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Vehicle'),
        content: Text(
            'Are you sure you want to delete "[${vehicle.unitCode}] ${vehicle.displayName}"? This cannot be undone.'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: FilledButton.styleFrom(
                backgroundColor: AppColors.danger),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      try {
        await ref
            .read(vehiclesProvider.notifier)
            .deleteVehicle(vehicle.id);
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text(
                    '"[${vehicle.unitCode}] ${vehicle.displayName}" deleted')),
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
  final String? selected; // null = All
  final ValueChanged<String?> onChanged;

  const _StatusFilter({required this.selected, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Container(
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
                label: 'Active',
                selected: selected == 'active',
                onTap: () => onChanged('active')),
            _Pill(
                label: 'Maintenance',
                selected: selected == 'inmaintenance',
                onTap: () => onChanged('inmaintenance')),
            _Pill(
                label: 'Out of Service',
                selected: selected == 'outofservice',
                onTap: () => onChanged('outofservice')),
            _Pill(
                label: 'Retired',
                selected: selected == 'retired',
                onTap: () => onChanged('retired')),
          ],
        ),
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

// ── Change status dialog ──────────────────────────────────────────────────────

class _ChangeStatusDialog extends StatelessWidget {
  final Vehicle vehicle;
  final ValueChanged<String> onSetStatus;

  const _ChangeStatusDialog({
    required this.vehicle,
    required this.onSetStatus,
  });

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text('Change Status — ${vehicle.unitCode}'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            'Select the new status for ${vehicle.displayName}.',
            style: const TextStyle(
                fontSize: 13, color: AppColors.brandGray),
          ),
          const SizedBox(height: 16),
          _StatusOption(
            label: 'Active',
            description: 'Vehicle is in service and available',
            color: AppColors.success,
            onTap: () => onSetStatus('Active'),
          ),
          const SizedBox(height: 8),
          _StatusOption(
            label: 'In Maintenance',
            description: 'Vehicle is undergoing scheduled maintenance',
            color: AppColors.warning,
            onTap: () => onSetStatus('InMaintenance'),
          ),
          const SizedBox(height: 8),
          _StatusOption(
            label: 'Retired',
            description: 'Vehicle is permanently decommissioned',
            color: AppColors.brandGray,
            onTap: () => onSetStatus('Retired'),
          ),
        ],
      ),
      actions: [
        TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel')),
      ],
    );
  }
}

class _StatusOption extends StatelessWidget {
  final String label;
  final String description;
  final Color color;
  final VoidCallback onTap;

  const _StatusOption({
    required this.label,
    required this.description,
    required this.color,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(10),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.06),
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: color.withValues(alpha: 0.25)),
        ),
        child: Row(
          children: [
            Container(
              width: 10,
              height: 10,
              decoration: BoxDecoration(
                  shape: BoxShape.circle, color: color),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(label,
                      style: TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 13,
                          color: color)),
                  Text(description,
                      style: const TextStyle(
                          fontSize: 11, color: AppColors.brandGray)),
                ],
              ),
            ),
            Icon(Icons.arrow_forward_ios_rounded, size: 12, color: color),
          ],
        ),
      ),
    );
  }
}

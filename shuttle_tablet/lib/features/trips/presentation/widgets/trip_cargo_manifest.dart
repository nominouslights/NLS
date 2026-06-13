import 'package:flutter/material.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip.dart';
import '../../domain/entities/trip_cargo_item.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../../domain/usecases/remove_cargo_item_usecase.dart';
import 'add_cargo_sheet.dart';

class TripCargoManifest extends StatelessWidget {
  final Trip trip;
  final VoidCallback onRefresh;

  const TripCargoManifest({
    super.key,
    required this.trip,
    required this.onRefresh,
  });

  bool get _isTerminal =>
      trip.status == TripStatus.completed ||
      trip.status == TripStatus.cancelled;

  @override
  Widget build(BuildContext context) {
    final items = trip.cargoItems;
    final boxCount = items
        .where((c) => c.cargoType == TripCargoType.box)
        .fold<int>(0, (sum, c) => sum + c.quantity);
    final palletCount = items
        .where((c) => c.cargoType == TripCargoType.pallet)
        .fold<int>(0, (sum, c) => sum + c.quantity);
    final countLabel = 'Cargo (${boxCount} boxes, $palletCount pallets)';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Icon(Icons.inventory_2_outlined,
                size: 16, color: AppColors.primary),
            const SizedBox(width: 6),
            Text(
              countLabel,
              style: const TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w700,
                color: AppColors.textPrimary,
                letterSpacing: 0.3,
              ),
            ),
            const Spacer(),
            if (!_isTerminal)
              TextButton.icon(
                onPressed: () => showAddCargoSheet(
                  context,
                  tripId: trip.id,
                  onRefresh: onRefresh,
                ),
                icon: const Icon(Icons.add_box_outlined, size: 16),
                label: const Text('Add'),
                style: TextButton.styleFrom(
                    foregroundColor: const Color(0xFF0F766E)),
              ),
          ],
        ),
        const SizedBox(height: 4),
        if (items.isEmpty)
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: const Color(0xFFF9FAFB),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: const Color(0xFFE5E7EB)),
            ),
            child: const Center(
              child: Text(
                'No cargo on manifest',
                style: TextStyle(color: AppColors.brandGray, fontSize: 13),
              ),
            ),
          )
        else
          ...items.map(
            (c) => _CargoRow(
              item: c,
              tripId: trip.id,
              onRefresh: onRefresh,
              readOnly: _isTerminal,
            ),
          ),
      ],
    );
  }
}

class _CargoRow extends StatelessWidget {
  final TripCargoItem item;
  final String tripId;
  final VoidCallback onRefresh;
  final bool readOnly;

  const _CargoRow({
    required this.item,
    required this.tripId,
    required this.onRefresh,
    this.readOnly = false,
  });

  @override
  Widget build(BuildContext context) {
    final icon = item.cargoType == TripCargoType.pallet
        ? Icons.view_module_rounded
        : Icons.inventory_2_outlined;

    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Row(
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.1),
              shape: BoxShape.circle,
            ),
            child: Icon(icon, size: 18, color: AppColors.primary),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.description?.isNotEmpty == true
                      ? item.description!
                      : item.typeLabel,
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 14,
                    color: Color(0xFF111827),
                  ),
                ),
                Row(
                  children: [
                    Text(
                      '${item.typeLabel} · Qty ${item.quantity}',
                      style: const TextStyle(
                          fontSize: 12, color: AppColors.textSecondary),
                    ),
                    if (item.weightKg != null) ...[
                      const Text(' · ',
                          style: TextStyle(
                              fontSize: 12,
                              color: AppColors.textSecondary)),
                      Text('${item.weightKg!.toStringAsFixed(1)} kg',
                          style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.textSecondary)),
                    ],
                    if (item.charge != null) ...[
                      const Text(' · ',
                          style: TextStyle(
                              fontSize: 12,
                              color: AppColors.textSecondary)),
                      Text('\$${item.charge!.toStringAsFixed(2)}',
                          style: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: AppColors.primary)),
                    ],
                  ],
                ),
                if (item.isHazmat || item.isSecured)
                  Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Row(
                      children: [
                        if (item.isHazmat)
                          _CargoBadge(
                              label: 'HAZMAT', color: AppColors.danger),
                        if (item.isSecured)
                          _CargoBadge(
                              label: 'SECURED', color: AppColors.success),
                      ],
                    ),
                  ),
              ],
            ),
          ),
          if (!readOnly)
            IconButton(
              onPressed: () => _confirmRemove(context),
              icon: const Icon(Icons.delete_outline_rounded,
                  size: 18, color: AppColors.danger),
              tooltip: 'Remove',
            ),
        ],
      ),
    );
  }

  Future<void> _confirmRemove(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Remove Cargo'),
        content: Text('Remove this ${item.typeLabel.toLowerCase()} from the manifest?'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
            child: const Text('Remove'),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;

    final result = await sl<RemoveCargoItemUseCase>()(RemoveCargoItemParams(
      tripId: tripId,
      cargoItemId: item.id,
    ));
    result.fold(
      (f) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text(f.message),
                backgroundColor: AppColors.danger),
          );
        }
      },
      (_) => onRefresh(),
    );
  }
}

class _CargoBadge extends StatelessWidget {
  final String label;
  final Color color;
  const _CargoBadge({required this.label, required this.color});

  @override
  Widget build(BuildContext context) => Container(
        margin: const EdgeInsets.only(right: 4),
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(4),
          border: Border.all(color: color.withValues(alpha: 0.35)),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 9,
            fontWeight: FontWeight.w800,
            color: color,
            letterSpacing: 0.5,
          ),
        ),
      );
}

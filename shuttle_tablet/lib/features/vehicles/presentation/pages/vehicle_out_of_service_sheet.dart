import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/vehicle.dart';
import '../providers/vehicles_provider.dart';

class VehicleOutOfServiceSheet extends ConsumerStatefulWidget {
  final Vehicle vehicle;

  const VehicleOutOfServiceSheet({super.key, required this.vehicle});

  @override
  ConsumerState<VehicleOutOfServiceSheet> createState() =>
      _VehicleOutOfServiceSheetState();
}

class _VehicleOutOfServiceSheetState
    extends ConsumerState<VehicleOutOfServiceSheet> {
  final _reasonCtrl = TextEditingController();
  bool _submitting = false;

  @override
  void dispose() {
    _reasonCtrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final reason = _reasonCtrl.text.trim();
    if (reason.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Reason is required.')),
      );
      return;
    }
    setState(() => _submitting = true);
    try {
      await ref
          .read(vehiclesProvider.notifier)
          .setOutOfService(widget.vehicle.id, reason);
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        setState(() => _submitting = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text(e.toString()),
              backgroundColor: AppColors.danger),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final keyboardHeight = MediaQuery.of(context).viewInsets.bottom;
    final safeBottom = MediaQuery.of(context).padding.bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: keyboardHeight),
      child: Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
        ),
        padding: EdgeInsets.fromLTRB(
            20, 12, 20, keyboardHeight > 0 ? 16 : safeBottom + 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Handle
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: const Color(0xFFD1D5DB),
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: 16),
            // Header
            Row(
              children: [
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: AppColors.danger.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: const Icon(Icons.block_rounded,
                      color: AppColors.danger, size: 20),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Mark Unit Out of Service',
                        style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                            color: Color(0xFF111827)),
                      ),
                      Text(
                        '[${widget.vehicle.unitCode}] ${widget.vehicle.displayName}',
                        style: const TextStyle(
                            fontSize: 13, color: AppColors.brandGray),
                      ),
                    ],
                  ),
                ),
                IconButton(
                  icon: const Icon(Icons.close_rounded),
                  onPressed: () => Navigator.of(context).pop(),
                ),
              ],
            ),
            const SizedBox(height: 20),
            // Warning banner
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.danger.withValues(alpha: 0.06),
                borderRadius: BorderRadius.circular(10),
                border: Border.all(
                    color: AppColors.danger.withValues(alpha: 0.2)),
              ),
              child: const Row(
                children: [
                  Icon(Icons.warning_amber_rounded,
                      size: 18, color: AppColors.danger),
                  SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'This unit will be removed from active service. A reason is required.',
                      style: TextStyle(
                          fontSize: 12, color: AppColors.danger),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),
            // Reason field
            TextFormField(
              controller: _reasonCtrl,
              maxLines: 3,
              decoration: InputDecoration(
                labelText: 'Reason *',
                hintText:
                    'e.g. Engine requires rebuild, transmission failure…',
                border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12)),
                alignLabelWithHint: true,
              ),
            ),
            const SizedBox(height: 20),
            // Buttons
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed:
                        _submitting ? null : () => Navigator.of(context).pop(),
                    child: const Text('Cancel'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  flex: 2,
                  child: FilledButton(
                    onPressed: _submitting ? null : _submit,
                    style: FilledButton.styleFrom(
                        backgroundColor: AppColors.danger),
                    child: _submitting
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(
                                strokeWidth: 2, color: Colors.white))
                        : const Text('Mark Out of Service'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

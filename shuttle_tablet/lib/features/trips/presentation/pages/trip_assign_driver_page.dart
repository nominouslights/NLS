import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../drivers/presentation/providers/drivers_provider.dart';
import '../../domain/entities/trip.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../providers/trip_form_provider.dart';
import '../providers/trips_provider.dart';

class TripAssignDriverPage extends ConsumerStatefulWidget {
  final Trip trip;
  const TripAssignDriverPage({super.key, required this.trip});

  @override
  ConsumerState<TripAssignDriverPage> createState() =>
      _TripAssignDriverPageState();
}

class _TripAssignDriverPageState extends ConsumerState<TripAssignDriverPage> {
  String? _selectedDriverId;
  bool _isSaving = false;

  @override
  void initState() {
    super.initState();
    _selectedDriverId = widget.trip.driverId;
  }

  Future<void> _save({bool dispatch = false}) async {
    if (_selectedDriverId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select a driver')),
      );
      return;
    }
    setState(() => _isSaving = true);
    try {
      await ref.read(tripFormProvider).assignDriver(
            widget.trip.id,
            AssignDriverParams(
              driverId: _selectedDriverId!,
              vehicleType: widget.trip.vehicleType,
            ),
          );
      if (dispatch) {
        if (!widget.trip.canDispatch) {
          throw Exception(Trip.dispatchManifestMessage);
        }
        await ref.read(tripFormProvider).dispatchTrip(widget.trip.id);
      }
      ref.invalidate(tripDetailProvider(widget.trip.id));
      ref.invalidate(tripsProvider);
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        final message = e.toString().replaceFirst('Exception: ', '');
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(message), backgroundColor: AppColors.danger),
        );
      }
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final driversAsync = ref.watch(driversProvider);
    final trip = widget.trip;
    final canDispatch = trip.status == TripStatus.scheduled &&
        _selectedDriverId != null &&
        trip.canDispatch;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          'Assign Driver',
          style: TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w700,
            color: AppColors.textPrimary,
          ),
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: const Color(0xFFE5E7EB)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    trip.purchaseOrderNumber != null
                        ? 'PO# ${trip.purchaseOrderNumber}'
                        : 'Trip',
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    DateFormat('EEEE, MMM d yyyy · h:mm a')
                        .format(trip.scheduledAt.toLocal()),
                    style: const TextStyle(
                      fontSize: 13,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  if (trip.firstStopLocation != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      trip.firstStopLocation!,
                      style: const TextStyle(fontSize: 13),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 24),
            const Text(
              'Driver',
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: 8),
            driversAsync.when(
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => Text(
                'Failed to load drivers: $e',
                style: const TextStyle(color: AppColors.danger),
              ),
              data: (drivers) => DropdownButtonFormField<String>(
                value: _selectedDriverId,
                decoration: _inputDecoration('Select driver'),
                isExpanded: true,
                items: drivers
                    .map((d) => DropdownMenuItem(
                          value: d.id,
                          child: Text(d.fullName),
                        ))
                    .toList(),
                onChanged: _isSaving
                    ? null
                    : (v) => setState(() => _selectedDriverId = v),
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        child: Container(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
          decoration: const BoxDecoration(
            color: Colors.white,
            border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
          ),
          child: Row(
            children: [
              OutlinedButton(
                onPressed: _isSaving ? null : () => Navigator.of(context).pop(),
                child: const Text('Cancel'),
              ),
              const Spacer(),
              OutlinedButton(
                onPressed: _isSaving ? null : () => _save(),
                child: _isSaving
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Save'),
              ),
              if (canDispatch) ...[
                const SizedBox(width: 10),
                FilledButton.icon(
                  onPressed: _isSaving ? null : () => _save(dispatch: true),
                  icon: const Icon(Icons.send_rounded, size: 16),
                  label: const Text('Save & Dispatch'),
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.primary,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  static InputDecoration _inputDecoration(String hint) => InputDecoration(
        hintText: hint,
        isDense: true,
        filled: true,
        fillColor: Colors.white,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFFD1D5DB)),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFFD1D5DB)),
        ),
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      );
}

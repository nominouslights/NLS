import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip_passenger.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../../domain/usecases/add_passenger_usecase.dart';

Future<void> showAddPassengerSheet(
  BuildContext context, {
  required String tripId,
  required VoidCallback onRefresh,
  bool isOverride = false,
}) {
  return showModalBottomSheet(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (ctx) => AddPassengerSheet(
      tripId: tripId,
      onRefresh: onRefresh,
      isOverride: isOverride,
    ),
  );
}

Future<void> showOverrideAddPassengerConfirm(
  BuildContext context, {
  required String tripId,
  required VoidCallback onRefresh,
}) async {
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: const Text('Override Manifest Lock'),
      content: const Text(
        'This trip has already departed. Adding a passenger now will flag them as a late addition and highlight them in red on the manifest.',
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(ctx, false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(ctx, true),
          style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
          child: const Text('Override & Add'),
        ),
      ],
    ),
  );
  if (confirmed != true || !context.mounted) return;
  await showAddPassengerSheet(
    context,
    tripId: tripId,
    onRefresh: onRefresh,
    isOverride: true,
  );
}

class AddPassengerSheet extends StatefulWidget {
  final String tripId;
  final VoidCallback onRefresh;
  final bool isOverride;

  const AddPassengerSheet({
    super.key,
    required this.tripId,
    required this.onRefresh,
    this.isOverride = false,
  });

  @override
  State<AddPassengerSheet> createState() => _AddPassengerSheetState();
}

class _AddPassengerSheetState extends State<AddPassengerSheet> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _seatController = TextEditingController();
  PassengerPaymentStatus _paymentStatus = PassengerPaymentStatus.tentative;
  bool _saving = false;

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _seatController.dispose();
    super.dispose();
  }

  Future<void> _confirm() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);

    final result = await sl<AddPassengerUseCase>()(AddPassengerParams(
      tripId: widget.tripId,
      name: _nameController.text.trim(),
      phone: _phoneController.text.trim().isEmpty
          ? null
          : _phoneController.text.trim(),
      email: _emailController.text.trim().isEmpty
          ? null
          : _emailController.text.trim(),
      seatNumber: _seatController.text.trim().isEmpty
          ? null
          : int.tryParse(_seatController.text.trim()),
      paymentStatus: _paymentStatus,
      isAddedAfterDeparture: widget.isOverride,
    ));

    if (!mounted) return;
    setState(() => _saving = false);

    result.fold(
      (f) => ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(f.message), backgroundColor: AppColors.danger),
      ),
      (_) {
        widget.onRefresh();
        Navigator.of(context).pop();
      },
    );
  }

  Widget _label(String text) => Text(
        text,
        style: const TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.w600,
          color: Color(0xFF374151),
        ),
      );

  InputDecoration _inputDec(String hint) => InputDecoration(
        hintText: hint,
        isDense: true,
        filled: true,
        fillColor: const Color(0xFFF9FAFB),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
        ),
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      );

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;
    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      constraints: BoxConstraints(
        maxHeight: MediaQuery.sizeOf(context).height * 0.88,
      ),
      child: Form(
        key: _formKey,
        child: SingleChildScrollView(
          padding: EdgeInsets.fromLTRB(24, 20, 24, 24 + bottomInset),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(
                child: Container(
                  width: 40,
                  height: 4,
                  decoration: BoxDecoration(
                    color: const Color(0xFFE5E7EB),
                    borderRadius: BorderRadius.circular(2),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              Text(
                widget.isOverride ? 'Add Passenger (Override)' : 'Add Passenger',
                style: const TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                ),
              ),
              if (widget.isOverride) ...[
                const SizedBox(height: 6),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(
                    color: AppColors.danger.withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(8),
                    border:
                        Border.all(color: AppColors.danger.withValues(alpha: 0.3)),
                  ),
                  child: Row(
                    children: [
                      Icon(Icons.warning_amber_rounded,
                          size: 14, color: AppColors.danger),
                      const SizedBox(width: 6),
                      const Expanded(
                        child: Text(
                          'This passenger will be flagged as a late addition.',
                          style: TextStyle(
                            fontSize: 12,
                            color: AppColors.danger,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
              const SizedBox(height: 20),
              _label('Passenger Name *'),
              const SizedBox(height: 6),
              TextFormField(
                controller: _nameController,
                textCapitalization: TextCapitalization.words,
                decoration: _inputDec('e.g. Jane Smith'),
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? 'Name is required' : null,
              ),
              const SizedBox(height: 14),
              _label('Phone'),
              const SizedBox(height: 6),
              TextFormField(
                controller: _phoneController,
                keyboardType: TextInputType.phone,
                decoration: _inputDec('Phone number'),
              ),
              const SizedBox(height: 14),
              _label('Email'),
              const SizedBox(height: 6),
              TextFormField(
                controller: _emailController,
                keyboardType: TextInputType.emailAddress,
                decoration: _inputDec('For booking confirmation emails'),
                validator: (v) {
                  final value = v?.trim() ?? '';
                  if (value.isEmpty) return null;
                  if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(value)) {
                    return 'Enter a valid email address';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 14),
              _label('Seat Number'),
              const SizedBox(height: 6),
              TextFormField(
                controller: _seatController,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                decoration: _inputDec('Auto-assigned if blank'),
              ),
              const SizedBox(height: 14),
              _label('Payment Status'),
              const SizedBox(height: 8),
              SegmentedButton<PassengerPaymentStatus>(
                selected: {_paymentStatus},
                onSelectionChanged: (s) =>
                    setState(() => _paymentStatus = s.first),
                style: SegmentedButton.styleFrom(
                  selectedBackgroundColor:
                      const Color(0xFF0F766E).withValues(alpha: 0.1),
                  selectedForegroundColor: const Color(0xFF0F766E),
                ),
                segments: const [
                  ButtonSegment(
                    value: PassengerPaymentStatus.tentative,
                    label: Text('Tentative'),
                  ),
                  ButtonSegment(
                    value: PassengerPaymentStatus.confirmed,
                    label: Text('Confirmed'),
                  ),
                ],
              ),
              const SizedBox(height: 24),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed:
                          _saving ? null : () => Navigator.of(context).pop(),
                      child: const Text('Cancel'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: FilledButton(
                      onPressed: _saving ? null : _confirm,
                      style: FilledButton.styleFrom(
                          backgroundColor: const Color(0xFF0F766E)),
                      child: _saving
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(
                                  strokeWidth: 2, color: Colors.white),
                            )
                          : const Text('Add Passenger'),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

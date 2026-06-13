import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip_cargo_item.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../../domain/usecases/add_cargo_item_usecase.dart';

Future<void> showAddCargoSheet(
  BuildContext context, {
  required String tripId,
  required VoidCallback onRefresh,
}) {
  return showModalBottomSheet(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (ctx) => AddCargoSheet(tripId: tripId, onRefresh: onRefresh),
  );
}

class AddCargoSheet extends StatefulWidget {
  final String tripId;
  final VoidCallback onRefresh;

  const AddCargoSheet({
    super.key,
    required this.tripId,
    required this.onRefresh,
  });

  @override
  State<AddCargoSheet> createState() => _AddCargoSheetState();
}

class _AddCargoSheetState extends State<AddCargoSheet> {
  final _formKey = GlobalKey<FormState>();
  final _descriptionController = TextEditingController();
  final _quantityController = TextEditingController(text: '1');
  final _weightController = TextEditingController();
  final _chargeController = TextEditingController();
  TripCargoType _cargoType = TripCargoType.box;
  bool _isHazmat = false;
  bool _isSecured = false;
  bool _saving = false;

  @override
  void dispose() {
    _descriptionController.dispose();
    _quantityController.dispose();
    _weightController.dispose();
    _chargeController.dispose();
    super.dispose();
  }

  Future<void> _confirm() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);

    final qty = int.parse(_quantityController.text.trim());
    final weightKg = _weightController.text.trim().isNotEmpty
        ? double.tryParse(_weightController.text.trim())
        : null;
    final charge = _chargeController.text.trim().isNotEmpty
        ? double.tryParse(_chargeController.text.trim())
        : null;

    final result = await sl<AddCargoItemUseCase>()(AddCargoItemParams(
      tripId: widget.tripId,
      cargoType: _cargoType,
      description: _descriptionController.text.trim().isEmpty
          ? null
          : _descriptionController.text.trim(),
      quantity: qty,
      weightKg: weightKg,
      charge: charge,
      isHazmat: _isHazmat,
      isSecured: _isSecured,
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

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;
    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
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
              const Text(
                'Add Cargo',
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                ),
              ),
              const SizedBox(height: 20),
              const Text(
                'Cargo Type',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: Color(0xFF374151),
                ),
              ),
              const SizedBox(height: 8),
              SegmentedButton<TripCargoType>(
                selected: {_cargoType},
                onSelectionChanged: (s) =>
                    setState(() => _cargoType = s.first),
                segments: const [
                  ButtonSegment(
                    value: TripCargoType.box,
                    icon: Icon(Icons.inventory_2_outlined, size: 16),
                    label: Text('Box'),
                  ),
                  ButtonSegment(
                    value: TripCargoType.pallet,
                    icon: Icon(Icons.view_module_rounded, size: 16),
                    label: Text('Pallet'),
                  ),
                ],
              ),
              const SizedBox(height: 14),
              const Text(
                'Description',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: Color(0xFF374151),
                ),
              ),
              const SizedBox(height: 6),
              TextFormField(
                controller: _descriptionController,
                decoration: const InputDecoration(
                  hintText: 'e.g. Site equipment crate',
                  isDense: true,
                  filled: true,
                  fillColor: Color(0xFFF9FAFB),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.all(Radius.circular(10)),
                  ),
                ),
              ),
              const SizedBox(height: 14),
              const Text(
                'Quantity *',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: Color(0xFF374151),
                ),
              ),
              const SizedBox(height: 6),
              TextFormField(
                controller: _quantityController,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                validator: (v) {
                  if (v == null || v.trim().isEmpty) return 'Required';
                  final n = int.tryParse(v.trim());
                  if (n == null || n < 1) return 'Enter at least 1';
                  return null;
                },
                decoration: const InputDecoration(
                  hintText: '1',
                  isDense: true,
                  filled: true,
                  fillColor: Color(0xFFF9FAFB),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.all(Radius.circular(10)),
                  ),
                ),
              ),
              const SizedBox(height: 14),
              Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Weight (kg)',
                            style: TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                                color: Color(0xFF374151))),
                        const SizedBox(height: 6),
                        TextFormField(
                          controller: _weightController,
                          keyboardType: const TextInputType.numberWithOptions(
                              decimal: true),
                          decoration: const InputDecoration(
                            hintText: '0.0',
                            suffixText: 'kg',
                            isDense: true,
                            filled: true,
                            fillColor: Color(0xFFF9FAFB),
                            border: OutlineInputBorder(
                                borderRadius:
                                    BorderRadius.all(Radius.circular(10))),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Charge (\$)',
                            style: TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                                color: Color(0xFF374151))),
                        const SizedBox(height: 6),
                        TextFormField(
                          controller: _chargeController,
                          keyboardType: const TextInputType.numberWithOptions(
                              decimal: true),
                          decoration: const InputDecoration(
                            hintText: '0.00',
                            prefixText: '\$',
                            isDense: true,
                            filled: true,
                            fillColor: Color(0xFFF9FAFB),
                            border: OutlineInputBorder(
                                borderRadius:
                                    BorderRadius.all(Radius.circular(10))),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      title: const Text('Hazmat',
                          style: TextStyle(fontSize: 13)),
                      value: _isHazmat,
                      onChanged: (v) =>
                          setState(() => _isHazmat = v ?? false),
                      activeColor: AppColors.danger,
                      controlAffinity: ListTileControlAffinity.leading,
                    ),
                  ),
                  Expanded(
                    child: CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      title: const Text('Secured',
                          style: TextStyle(fontSize: 13)),
                      value: _isSecured,
                      onChanged: (v) =>
                          setState(() => _isSecured = v ?? false),
                      activeColor: AppColors.success,
                      controlAffinity: ListTileControlAffinity.leading,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),
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
                          : const Text('Add Cargo'),
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

import 'package:flutter/material.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip_stop.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../../domain/usecases/add_stop_usecase.dart';

Future<void> showAddStopSheet(
  BuildContext context, {
  required String tripId,
  required List<TripStop> stops,
  required VoidCallback onRefresh,
}) {
  return showModalBottomSheet(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (ctx) => AddStopSheet(
      tripId: tripId,
      stops: stops,
      onRefresh: onRefresh,
    ),
  );
}

class AddStopSheet extends StatefulWidget {
  final String tripId;
  final List<TripStop> stops;
  final VoidCallback onRefresh;

  const AddStopSheet({
    super.key,
    required this.tripId,
    required this.stops,
    required this.onRefresh,
  });

  @override
  State<AddStopSheet> createState() => _AddStopSheetState();
}

class _AddStopSheetState extends State<AddStopSheet> {
  final _formKey = GlobalKey<FormState>();
  final _locationController = TextEditingController();
  final _addressController = TextEditingController();
  late int _insertAtSequenceOrder;
  bool _saving = false;

  List<TripStop> get _sorted =>
      [...widget.stops]..sort((a, b) => a.sequenceOrder.compareTo(b.sequenceOrder));

  @override
  void initState() {
    super.initState();
    final maxSeq = widget.stops.isNotEmpty
        ? widget.stops.map((s) => s.sequenceOrder).reduce((a, b) => a > b ? a : b)
        : 0;
    _insertAtSequenceOrder = maxSeq + 1;
  }

  @override
  void dispose() {
    _locationController.dispose();
    _addressController.dispose();
    super.dispose();
  }

  List<_PositionOption> get _positionOptions {
    final sorted = _sorted;
    final options = <_PositionOption>[];

    if (sorted.isNotEmpty) {
      options.add(_PositionOption(
        label: 'At the beginning (before ${sorted.first.locationName})',
        insertAt: 1,
      ));
      for (var i = 0; i < sorted.length - 1; i++) {
        options.add(_PositionOption(
          label: 'After ${sorted[i].locationName}',
          insertAt: sorted[i].sequenceOrder + 1,
        ));
      }
      options.add(_PositionOption(
        label: 'At the end (after ${sorted.last.locationName})',
        insertAt: sorted.last.sequenceOrder + 1,
      ));
    } else {
      options.add(_PositionOption(label: 'First stop', insertAt: 1));
    }

    return options;
  }

  Future<void> _confirm() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);

    final address = _addressController.text.trim();
    final result = await sl<AddStopUseCase>()(AddStopParams(
      tripId: widget.tripId,
      insertAtSequenceOrder: _insertAtSequenceOrder,
      locationName: _locationController.text.trim(),
      address: address.isEmpty ? null : address,
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
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      );

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;
    final options = _positionOptions;

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
              const Text(
                'Add Stop',
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                ),
              ),
              const SizedBox(height: 20),
              _label('Position *'),
              const SizedBox(height: 6),
              DropdownButtonFormField<int>(
                key: ValueKey(_insertAtSequenceOrder),
                initialValue: _insertAtSequenceOrder,
                decoration: _inputDec('Select position'),
                items: options
                    .map((o) => DropdownMenuItem(
                          value: o.insertAt,
                          child: Text(o.label, overflow: TextOverflow.ellipsis),
                        ))
                    .toList(),
                onChanged: (v) {
                  if (v != null) setState(() => _insertAtSequenceOrder = v);
                },
              ),
              const SizedBox(height: 14),
              _label('Location Name *'),
              const SizedBox(height: 6),
              TextFormField(
                controller: _locationController,
                textCapitalization: TextCapitalization.words,
                decoration: _inputDec('e.g. Downtown Terminal'),
                validator: (v) => (v == null || v.trim().isEmpty)
                    ? 'Location name is required'
                    : null,
              ),
              const SizedBox(height: 14),
              _label('Address'),
              const SizedBox(height: 6),
              TextFormField(
                controller: _addressController,
                textCapitalization: TextCapitalization.sentences,
                decoration: _inputDec('Street address (optional)'),
              ),
              const SizedBox(height: 24),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: _saving ? null : () => Navigator.of(context).pop(),
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
                          : const Text('Add Stop'),
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

class _PositionOption {
  final String label;
  final int insertAt;
  const _PositionOption({required this.label, required this.insertAt});
}

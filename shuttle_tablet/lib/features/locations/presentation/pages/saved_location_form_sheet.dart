import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/saved_location.dart';
import '../../domain/repositories/i_location_repository.dart';
import '../providers/locations_provider.dart';

class SavedLocationFormSheet extends ConsumerStatefulWidget {
  final SavedLocation? location; // null = create

  const SavedLocationFormSheet({super.key, this.location});

  @override
  ConsumerState<SavedLocationFormSheet> createState() =>
      _SavedLocationFormSheetState();
}

class _SavedLocationFormSheetState
    extends ConsumerState<SavedLocationFormSheet> {
  final _formKey = GlobalKey<FormState>();
  final _nameCtrl = TextEditingController();
  final _addressCtrl = TextEditingController();
  final _latCtrl = TextEditingController();
  final _lngCtrl = TextEditingController();
  bool _submitting = false;

  bool get _isEditing => widget.location != null;

  @override
  void initState() {
    super.initState();
    if (_isEditing) _populate(widget.location!);
  }

  void _populate(SavedLocation loc) {
    _nameCtrl.text = loc.name;
    _addressCtrl.text = loc.address ?? '';
    _latCtrl.text = loc.latitude?.toString() ?? '';
    _lngCtrl.text = loc.longitude?.toString() ?? '';
  }

  @override
  void dispose() {
    _nameCtrl.dispose();
    _addressCtrl.dispose();
    _latCtrl.dispose();
    _lngCtrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _submitting = true);
    try {
      final lat = _latCtrl.text.trim().isEmpty
          ? null
          : double.parse(_latCtrl.text.trim());
      final lng = _lngCtrl.text.trim().isEmpty
          ? null
          : double.parse(_lngCtrl.text.trim());
      final notifier = ref.read(locationsProvider.notifier);
      if (_isEditing) {
        await notifier.updateLocation(
          widget.location!.id,
          UpdateLocationParams(
            name: _nameCtrl.text.trim(),
            address: _addressCtrl.text.trim().isEmpty
                ? null
                : _addressCtrl.text.trim(),
            latitude: lat,
            longitude: lng,
          ),
        );
      } else {
        await notifier.createLocation(
          CreateLocationParams(
            name: _nameCtrl.text.trim(),
            address: _addressCtrl.text.trim().isEmpty
                ? null
                : _addressCtrl.text.trim(),
            latitude: lat,
            longitude: lng,
          ),
        );
      }
      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Failed to save: $e'),
            backgroundColor: AppColors.danger,
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.of(context).viewInsets.bottom;
    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      padding: EdgeInsets.fromLTRB(24, 20, 24, 24 + bottomInset),
      child: Form(
        key: _formKey,
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
                  color: const Color(0xFFE5E7EB),
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: 16),
            Text(
              _isEditing ? 'Edit Location' : 'New Saved Location',
              style: const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w700,
                color: Color(0xFF111827),
              ),
            ),
            const SizedBox(height: 20),
            _label('Location Name *'),
            const SizedBox(height: 6),
            TextFormField(
              controller: _nameCtrl,
              textCapitalization: TextCapitalization.words,
              decoration: _inputDec('e.g. Shuttle Depot'),
              validator: (v) {
                if (v == null || v.trim().isEmpty) {
                  return 'Name is required';
                }
                if (v.trim().length > 200) {
                  return 'Name must be 200 characters or fewer';
                }
                return null;
              },
            ),
            const SizedBox(height: 14),
            _label('Address (optional)'),
            const SizedBox(height: 6),
            TextFormField(
              controller: _addressCtrl,
              textCapitalization: TextCapitalization.sentences,
              decoration: _inputDec('e.g. 123 Main St, Thunder Bay, ON'),
              maxLines: 2,
              validator: (v) {
                if (v != null && v.trim().length > 500) {
                  return 'Address must be 500 characters or fewer';
                }
                return null;
              },
            ),
            const SizedBox(height: 14),
            _label('Coordinates (optional)'),
            const SizedBox(height: 6),
            Row(
              children: [
                Expanded(
                  child: TextFormField(
                    controller: _latCtrl,
                    keyboardType: const TextInputType.numberWithOptions(
                        signed: true, decimal: true),
                    inputFormatters: [
                      FilteringTextInputFormatter.allow(
                          RegExp(r'^-?\d*\.?\d*')),
                    ],
                    decoration: _inputDec('Latitude').copyWith(
                      labelText: 'Latitude',
                      hintText: 'e.g. 43.6532',
                    ),
                    validator: _validateCoord(
                      label: 'Latitude',
                      min: -90,
                      max: 90,
                      otherCtrl: _lngCtrl,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: TextFormField(
                    controller: _lngCtrl,
                    keyboardType: const TextInputType.numberWithOptions(
                        signed: true, decimal: true),
                    inputFormatters: [
                      FilteringTextInputFormatter.allow(
                          RegExp(r'^-?\d*\.?\d*')),
                    ],
                    decoration: _inputDec('Longitude').copyWith(
                      labelText: 'Longitude',
                      hintText: 'e.g. -79.3832',
                    ),
                    validator: _validateCoord(
                      label: 'Longitude',
                      min: -180,
                      max: 180,
                      otherCtrl: _latCtrl,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 24),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed:
                        _submitting ? null : () => Navigator.pop(context),
                    child: const Text('Cancel'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: FilledButton(
                    onPressed: _submitting ? null : _submit,
                    style: FilledButton.styleFrom(
                        backgroundColor: AppColors.primary),
                    child: _submitting
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(
                                strokeWidth: 2, color: Colors.white),
                          )
                        : Text(_isEditing ? 'Save Changes' : 'Add Location'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
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

  FormFieldValidator<String> _validateCoord({
    required String label,
    required double min,
    required double max,
    required TextEditingController otherCtrl,
  }) =>
      (v) {
        final filled = v != null && v.trim().isNotEmpty;
        final otherFilled = otherCtrl.text.trim().isNotEmpty;
        if (!filled && !otherFilled) return null;
        if (!filled && otherFilled) {
          return 'Enter both coordinates or neither';
        }
        final parsed = double.tryParse(v!.trim());
        if (parsed == null) return 'Enter a valid number';
        if (parsed < min || parsed > max) {
          return '$label must be $min to $max';
        }
        return null;
      };
}

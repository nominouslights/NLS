import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/vehicle_inspection_record.dart';
import '../../domain/repositories/i_vehicle_repository.dart';
import '../providers/vehicle_records_provider.dart';

const _kInspectionTypes = [
  ('ProvincialSafety', 'Provincial Safety'),
  ('AnnualMechanical', 'Annual Mechanical'),
  ('InsuranceSurvey', 'Insurance Survey'),
  ('InternalQuality', 'Internal Quality'),
  ('DOT', 'DOT Inspection'),
];

const _kInspectionResults = [
  ('Pass', 'Pass'),
  ('PassWithConditions', 'Pass with Conditions'),
  ('Fail', 'Fail'),
];

class VehicleInspectionFormSheet extends ConsumerStatefulWidget {
  final String vehicleId;
  final VehicleInspectionRecord? record; // null = add

  const VehicleInspectionFormSheet({
    super.key,
    required this.vehicleId,
    this.record,
  });

  @override
  ConsumerState<VehicleInspectionFormSheet> createState() =>
      _VehicleInspectionFormSheetState();
}

class _VehicleInspectionFormSheetState
    extends ConsumerState<VehicleInspectionFormSheet> {
  final _formKey = GlobalKey<FormState>();

  String _inspectionType = 'ProvincialSafety';
  DateTime _inspectedAt = DateTime.now();
  DateTime? _expiresAt;
  final _inspectorCtrl = TextEditingController();
  final _facilityCtrl = TextEditingController();
  final _certCtrl = TextEditingController();
  String _result = 'Pass';
  final _deficienciesCtrl = TextEditingController();
  final _correctiveCtrl = TextEditingController();
  final _costCtrl = TextEditingController();

  bool get _isEditing => widget.record != null;

  @override
  void initState() {
    super.initState();
    if (_isEditing) _populate(widget.record!);
  }

  void _populate(VehicleInspectionRecord r) {
    _inspectionType = r.inspectionType;
    _inspectedAt = r.inspectedAt;
    _expiresAt = r.expiresAt;
    _inspectorCtrl.text = r.inspectorName ?? '';
    _facilityCtrl.text = r.inspectionFacility ?? '';
    _certCtrl.text = r.certificateNumber ?? '';
    _result = r.inspectionResult;
    _deficienciesCtrl.text = r.deficienciesNotes ?? '';
    _correctiveCtrl.text = r.correctiveActionNotes ?? '';
    _costCtrl.text = r.costDollars?.toStringAsFixed(2) ?? '';
  }

  @override
  void dispose() {
    for (final c in [
      _inspectorCtrl, _facilityCtrl, _certCtrl,
      _deficienciesCtrl, _correctiveCtrl, _costCtrl,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    final params = AddInspectionRecordParams(
      inspectionType: _inspectionType,
      inspectedAt: _inspectedAt,
      expiresAt: _expiresAt,
      inspectorName: _inspectorCtrl.text.trim().isNotEmpty
          ? _inspectorCtrl.text.trim()
          : null,
      inspectionFacility: _facilityCtrl.text.trim().isNotEmpty
          ? _facilityCtrl.text.trim()
          : null,
      certificateNumber: _certCtrl.text.trim().isNotEmpty
          ? _certCtrl.text.trim()
          : null,
      inspectionResult: _result,
      deficienciesNotes: _deficienciesCtrl.text.trim().isNotEmpty
          ? _deficienciesCtrl.text.trim()
          : null,
      correctiveActionNotes: _correctiveCtrl.text.trim().isNotEmpty
          ? _correctiveCtrl.text.trim()
          : null,
      costDollars: _costCtrl.text.trim().isNotEmpty
          ? double.tryParse(_costCtrl.text.trim())
          : null,
    );

    try {
      final notifier =
          ref.read(vehicleRecordsProvider(widget.vehicleId).notifier);
      if (_isEditing) {
        await notifier.updateInspectionRecord(widget.record!.id, params);
      } else {
        await notifier.addInspectionRecord(params);
      }
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
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
    final isLoading =
        ref.watch(vehicleRecordsProvider(widget.vehicleId)).isLoading;
    final keyboardHeight = MediaQuery.of(context).viewInsets.bottom;
    final safeBottom = MediaQuery.of(context).padding.bottom;
    final deficienciesRequired = _result.toLowerCase() != 'pass';

    return Padding(
      padding: EdgeInsets.only(bottom: keyboardHeight),
      child: Container(
        height: MediaQuery.of(context).size.height * 0.85,
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
        ),
        child: Column(
          children: [
            const SizedBox(height: 12),
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
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 16, 12, 0),
              child: Row(
                children: [
                  Text(
                    _isEditing
                        ? 'Edit Inspection Record'
                        : 'New Inspection Record',
                    style: const TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.w800,
                        color: Color(0xFF111827)),
                  ),
                  const Spacer(),
                  IconButton(
                    icon: const Icon(Icons.close_rounded),
                    onPressed: () => Navigator.of(context).pop(),
                  ),
                ],
              ),
            ),
            const Divider(height: 1),
            Expanded(
              child: Form(
                key: _formKey,
                child: ListView(
                  padding: const EdgeInsets.all(20),
                  children: [
                    // Inspection Type
                    DropdownButtonFormField<String>(
                      value: _inspectionType,
                      decoration: InputDecoration(
                        labelText: 'Inspection Type *',
                        border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12)),
                      ),
                      items: _kInspectionTypes
                          .map((e) => DropdownMenuItem(
                                value: e.$1,
                                child: Text(e.$2),
                              ))
                          .toList(),
                      onChanged: (v) =>
                          setState(() => _inspectionType = v!),
                    ),
                    const SizedBox(height: 14),
                    // Inspected At
                    _DatePickerField(
                      label: 'Inspection Date *',
                      value: _inspectedAt,
                      firstDate: DateTime(2010),
                      lastDate: DateTime.now().add(const Duration(days: 1)),
                      onChanged: (d) => setState(() => _inspectedAt = d!),
                    ),
                    const SizedBox(height: 14),
                    // Expires At
                    _DatePickerField(
                      label: 'Expiry Date (optional)',
                      value: _expiresAt,
                      firstDate: DateTime.now(),
                      lastDate: DateTime(2040),
                      onChanged: (d) => setState(() => _expiresAt = d),
                      nullable: true,
                    ),
                    const SizedBox(height: 14),
                    // Inspector & Facility
                    Row(
                      children: [
                        Expanded(
                          child: _FieldWidget(
                            controller: _inspectorCtrl,
                            label: 'Inspector Name',
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: _FieldWidget(
                            controller: _facilityCtrl,
                            label: 'Inspection Facility',
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    // Certificate & Cost
                    Row(
                      children: [
                        Expanded(
                          child: _FieldWidget(
                            controller: _certCtrl,
                            label: 'Certificate #',
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: _FieldWidget(
                            controller: _costCtrl,
                            label: 'Cost (\$)',
                            keyboardType: const TextInputType.numberWithOptions(
                                decimal: true),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    // Result
                    DropdownButtonFormField<String>(
                      value: _result,
                      decoration: InputDecoration(
                        labelText: 'Inspection Result *',
                        border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12)),
                      ),
                      items: _kInspectionResults
                          .map((e) => DropdownMenuItem(
                                value: e.$1,
                                child: Text(e.$2),
                              ))
                          .toList(),
                      onChanged: (v) =>
                          setState(() => _result = v!),
                    ),
                    // Deficiencies (required if fail/pass-with-conditions)
                    if (deficienciesRequired) ...[
                      const SizedBox(height: 14),
                      TextFormField(
                        controller: _deficienciesCtrl,
                        maxLines: 3,
                        decoration: InputDecoration(
                          labelText: 'Deficiencies *',
                          hintText:
                              'Describe any deficiencies found…',
                          border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12)),
                          alignLabelWithHint: true,
                        ),
                        validator: (v) => (v == null || v.trim().isEmpty)
                            ? 'Deficiencies are required when result is not Pass'
                            : null,
                      ),
                      const SizedBox(height: 14),
                      TextFormField(
                        controller: _correctiveCtrl,
                        maxLines: 2,
                        decoration: InputDecoration(
                          labelText: 'Corrective Actions (optional)',
                          border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12)),
                          alignLabelWithHint: true,
                        ),
                      ),
                    ],
                    const SizedBox(height: 8),
                  ],
                ),
              ),
            ),
            // Footer
            Container(
              padding: EdgeInsets.fromLTRB(
                  16, 12, 16, keyboardHeight > 0 ? 12 : safeBottom + 12),
              decoration: const BoxDecoration(
                border:
                    Border(top: BorderSide(color: Color(0xFFE5E7EB))),
              ),
              child: Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () => Navigator.of(context).pop(),
                      child: const Text('Cancel'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    flex: 2,
                    child: FilledButton(
                      onPressed: isLoading ? null : _submit,
                      style: FilledButton.styleFrom(
                          backgroundColor: AppColors.primary),
                      child: isLoading
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: Colors.white))
                          : Text(_isEditing
                              ? 'Save Changes'
                              : 'Add Inspection'),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ── Shared ────────────────────────────────────────────────────────────────────

class _FieldWidget extends StatelessWidget {
  final TextEditingController controller;
  final String label;
  final TextInputType? keyboardType;

  const _FieldWidget({
    required this.controller,
    required this.label,
    this.keyboardType,
  });

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      keyboardType: keyboardType,
      decoration: InputDecoration(
        labelText: label,
        border:
            OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
      ),
    );
  }
}

class _DatePickerField extends StatelessWidget {
  final String label;
  final DateTime? value;
  final DateTime firstDate;
  final DateTime lastDate;
  final ValueChanged<DateTime?> onChanged;
  final bool nullable;

  const _DatePickerField({
    required this.label,
    required this.value,
    required this.firstDate,
    required this.lastDate,
    required this.onChanged,
    this.nullable = false,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: InkWell(
            onTap: () async {
              final picked = await showDatePicker(
                context: context,
                initialDate:
                    value ?? DateTime.now().add(const Duration(days: 365)),
                firstDate: firstDate,
                lastDate: lastDate,
              );
              if (picked != null) onChanged(picked);
            },
            borderRadius: BorderRadius.circular(12),
            child: InputDecorator(
              decoration: InputDecoration(
                labelText: label,
                suffixIcon:
                    const Icon(Icons.calendar_today_outlined, size: 18),
                border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12)),
              ),
              child: Text(
                value != null
                    ? DateFormat('MMM d, yyyy').format(value!)
                    : 'Tap to select…',
                style: TextStyle(
                  fontSize: 14,
                  color: value != null
                      ? const Color(0xFF111827)
                      : AppColors.brandGray,
                ),
              ),
            ),
          ),
        ),
        if (nullable && value != null) ...[
          const SizedBox(width: 8),
          IconButton(
            onPressed: () => onChanged(null),
            icon: const Icon(Icons.clear_rounded,
                size: 18, color: AppColors.brandGray),
          ),
        ],
      ],
    );
  }
}

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/vehicle_service_record.dart';
import '../../domain/repositories/i_vehicle_repository.dart';
import '../providers/vehicle_records_provider.dart';

// ── Enum label helpers ─────────────────────────────────────────────────────────

const _kCategories = [
  ('FluidChange', 'Fluid Change'),
  ('TireService', 'Tire Service'),
  ('BrakeService', 'Brake Service'),
  ('EngineMaintenance', 'Engine Maintenance'),
  ('TransmissionService', 'Transmission Service'),
  ('ElectricalRepair', 'Electrical Repair'),
  ('BodyWork', 'Body Work'),
  ('PreventativeMaintenance', 'Preventative Maintenance'),
  ('UnplannedRepair', 'Unplanned Repair'),
  ('Other', 'Other'),
];

const _kFluidTypes = [
  ('EngineOil', 'Engine Oil'),
  ('Coolant', 'Coolant'),
  ('TransmissionFluid', 'Transmission Fluid'),
  ('BrakeFluid', 'Brake Fluid'),
  ('PowerSteeringFluid', 'Power Steering Fluid'),
  ('WindowWasherFluid', 'Window Washer Fluid'),
  ('DifferentialFluid', 'Differential Fluid'),
];

const _kStatuses = [
  ('Scheduled', 'Scheduled'),
  ('InProgress', 'In Progress'),
  ('Completed', 'Completed'),
  ('Deferred', 'Deferred'),
  ('Cancelled', 'Cancelled'),
];

const _kPriorities = [
  ('Routine', 'Routine'),
  ('Important', 'Important'),
  ('Urgent', 'Urgent'),
  ('Critical', 'Critical'),
];

class VehicleServiceRecordFormSheet extends ConsumerStatefulWidget {
  final String vehicleId;
  final VehicleServiceRecord? record; // null = add

  const VehicleServiceRecordFormSheet({
    super.key,
    required this.vehicleId,
    this.record,
  });

  @override
  ConsumerState<VehicleServiceRecordFormSheet> createState() =>
      _VehicleServiceRecordFormSheetState();
}

class _VehicleServiceRecordFormSheetState
    extends ConsumerState<VehicleServiceRecordFormSheet> {
  final _formKey = GlobalKey<FormState>();

  // Fields
  String _category = 'PreventativeMaintenance';
  String? _fluidType;
  final _titleCtrl = TextEditingController();
  final _descCtrl = TextEditingController();
  bool _isPlanned = true;
  String _status = 'Scheduled';
  String _priority = 'Routine';
  DateTime? _scheduledDate;
  final _odometerCtrl = TextEditingController();
  final _estimatedCostCtrl = TextEditingController();
  final _providerCtrl = TextEditingController();
  final _technicianCtrl = TextEditingController();
  final _partsNotesCtrl = TextEditingController();
  bool _isWarrantyWork = false;
  DateTime? _nextServiceDate;
  final _nextOdometerCtrl = TextEditingController();

  bool get _isEditing => widget.record != null;

  @override
  void initState() {
    super.initState();
    if (_isEditing) _populate(widget.record!);
  }

  void _populate(VehicleServiceRecord r) {
    _category = r.serviceCategory;
    _fluidType = r.fluidType;
    _titleCtrl.text = r.title;
    _descCtrl.text = r.description ?? '';
    _isPlanned = r.isPlanned;
    _status = r.serviceStatus;
    _priority = r.priority;
    _scheduledDate = r.scheduledDate;
    _odometerCtrl.text = r.odometerAtService?.toString() ?? '';
    _estimatedCostCtrl.text = r.estimatedCostDollars?.toStringAsFixed(2) ?? '';
    _providerCtrl.text = r.serviceProvider ?? '';
    _technicianCtrl.text = r.technicianName ?? '';
    _partsNotesCtrl.text = r.partsNotes ?? '';
    _isWarrantyWork = r.isWarrantyWork;
    _nextServiceDate = r.nextServiceDueDateUtc;
    _nextOdometerCtrl.text = r.nextServiceDueOdometerKm?.toString() ?? '';
  }

  @override
  void dispose() {
    for (final c in [
      _titleCtrl, _descCtrl, _odometerCtrl, _estimatedCostCtrl,
      _providerCtrl, _technicianCtrl, _partsNotesCtrl, _nextOdometerCtrl,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    final params = AddServiceRecordParams(
      serviceCategory: _category,
      fluidType: _category == 'FluidChange' ? _fluidType : null,
      title: _titleCtrl.text.trim(),
      description: _descCtrl.text.trim().isNotEmpty ? _descCtrl.text.trim() : null,
      isPlanned: _isPlanned,
      serviceStatus: _status,
      priority: _priority,
      scheduledDate: _scheduledDate,
      odometerAtService: _odometerCtrl.text.trim().isNotEmpty
          ? int.tryParse(_odometerCtrl.text.trim())
          : null,
      estimatedCostDollars: _estimatedCostCtrl.text.trim().isNotEmpty
          ? double.tryParse(_estimatedCostCtrl.text.trim())
          : null,
      serviceProvider: _providerCtrl.text.trim().isNotEmpty ? _providerCtrl.text.trim() : null,
      technicianName: _technicianCtrl.text.trim().isNotEmpty ? _technicianCtrl.text.trim() : null,
      partsNotes: _partsNotesCtrl.text.trim().isNotEmpty ? _partsNotesCtrl.text.trim() : null,
      isWarrantyWork: _isWarrantyWork,
      nextServiceDueDateUtc: _nextServiceDate,
      nextServiceDueOdometerKm: _nextOdometerCtrl.text.trim().isNotEmpty
          ? int.tryParse(_nextOdometerCtrl.text.trim())
          : null,
    );

    try {
      final notifier = ref.read(vehicleRecordsProvider(widget.vehicleId).notifier);
      if (_isEditing) {
        await notifier.updateServiceRecord(widget.record!.id, params);
      } else {
        await notifier.addServiceRecord(params);
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

    return Padding(
      padding: EdgeInsets.only(bottom: keyboardHeight),
      child: Container(
        height: MediaQuery.of(context).size.height * 0.88,
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
            // Header
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 16, 12, 0),
              child: Row(
                children: [
                  Text(
                    _isEditing ? 'Edit Service Record' : 'New Service Record',
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
            // Body
            Expanded(
              child: Form(
                key: _formKey,
                child: ListView(
                  padding: const EdgeInsets.all(20),
                  children: [
                    // Category & Fluid Type
                    _buildDropdown<String>(
                      label: 'Service Category *',
                      value: _category,
                      items: _kCategories
                          .map((e) => DropdownMenuItem(
                                value: e.$1,
                                child: Text(e.$2),
                              ))
                          .toList(),
                      onChanged: (v) =>
                          setState(() => _category = v!),
                    ),
                    if (_category == 'FluidChange') ...[
                      const SizedBox(height: 14),
                      _buildDropdown<String?>(
                        label: 'Fluid Type',
                        value: _fluidType,
                        items: [
                          const DropdownMenuItem(value: null, child: Text('Select…')),
                          ..._kFluidTypes.map((e) => DropdownMenuItem(
                                value: e.$1,
                                child: Text(e.$2),
                              )),
                        ],
                        onChanged: (v) => setState(() => _fluidType = v),
                      ),
                    ],
                    const SizedBox(height: 14),
                    // Title
                    _Field(
                        controller: _titleCtrl,
                        label: 'Title',
                        required: true,
                        hintText: 'e.g. Oil Change — 5W-30'),
                    const SizedBox(height: 14),
                    // Description
                    TextFormField(
                      controller: _descCtrl,
                      maxLines: 2,
                      decoration: InputDecoration(
                        labelText: 'Description (optional)',
                        border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12)),
                      ),
                    ),
                    const SizedBox(height: 14),
                    // Planned / Unplanned toggle
                    _SectionLabel(label: 'Work Type'),
                    const SizedBox(height: 6),
                    Row(
                      children: [
                        _ToggleChip(
                          label: 'Planned',
                          selected: _isPlanned,
                          onTap: () => setState(() => _isPlanned = true),
                        ),
                        const SizedBox(width: 8),
                        _ToggleChip(
                          label: 'Unplanned',
                          selected: !_isPlanned,
                          onTap: () => setState(() => _isPlanned = false),
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    // Status & Priority
                    Row(
                      children: [
                        Expanded(
                          child: _buildDropdown<String>(
                            label: 'Status',
                            value: _status,
                            items: _kStatuses
                                .map((e) => DropdownMenuItem(
                                      value: e.$1,
                                      child: Text(e.$2),
                                    ))
                                .toList(),
                            onChanged: (v) =>
                                setState(() => _status = v!),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: _buildDropdown<String>(
                            label: 'Priority',
                            value: _priority,
                            items: _kPriorities
                                .map((e) => DropdownMenuItem(
                                      value: e.$1,
                                      child: Text(e.$2),
                                    ))
                                .toList(),
                            onChanged: (v) =>
                                setState(() => _priority = v!),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    // Scheduled date
                    _DatePicker(
                      label: 'Scheduled Date',
                      value: _scheduledDate,
                      firstDate: DateTime(2020),
                      lastDate: DateTime(2040),
                      onChanged: (d) => setState(() => _scheduledDate = d),
                    ),
                    const SizedBox(height: 14),
                    // Odometer & Cost
                    Row(
                      children: [
                        Expanded(
                          child: _Field(
                            controller: _odometerCtrl,
                            label: 'Odometer (km)',
                            keyboardType: TextInputType.number,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: _Field(
                            controller: _estimatedCostCtrl,
                            label: 'Estimated Cost (\$)',
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    // Provider & Technician
                    Row(
                      children: [
                        Expanded(
                          child: _Field(
                            controller: _providerCtrl,
                            label: 'Service Provider',
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: _Field(
                            controller: _technicianCtrl,
                            label: 'Technician',
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    _Field(
                      controller: _partsNotesCtrl,
                      label: 'Parts Notes',
                    ),
                    const SizedBox(height: 14),
                    // Warranty toggle
                    Row(
                      children: [
                        const Expanded(
                          child: Text('Warranty Work',
                              style: TextStyle(fontSize: 14)),
                        ),
                        Switch(
                          value: _isWarrantyWork,
                          onChanged: (v) =>
                              setState(() => _isWarrantyWork = v),
                          activeColor: AppColors.primary,
                        ),
                      ],
                    ),
                    const Divider(height: 28),
                    const _SectionLabel(label: 'Next Service Due'),
                    const SizedBox(height: 10),
                    _DatePicker(
                      label: 'Next Service Date',
                      value: _nextServiceDate,
                      firstDate: DateTime.now(),
                      lastDate: DateTime(2040),
                      onChanged: (d) =>
                          setState(() => _nextServiceDate = d),
                    ),
                    const SizedBox(height: 14),
                    _Field(
                      controller: _nextOdometerCtrl,
                      label: 'Next Service Odometer (km)',
                      keyboardType: TextInputType.number,
                    ),
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
                              : 'Add Record'),
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

  Widget _buildDropdown<T>({
    required String label,
    required T value,
    required List<DropdownMenuItem<T>> items,
    required ValueChanged<T?> onChanged,
  }) {
    return DropdownButtonFormField<T>(
      value: value,
      decoration: InputDecoration(
        labelText: label,
        border:
            OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
        isDense: true,
      ),
      items: items,
      onChanged: onChanged,
    );
  }
}

// ── Shared form widgets ───────────────────────────────────────────────────────

class _SectionLabel extends StatelessWidget {
  final String label;
  const _SectionLabel({required this.label});

  @override
  Widget build(BuildContext context) {
    return Text(
      label.toUpperCase(),
      style: const TextStyle(
          fontSize: 10,
          fontWeight: FontWeight.w700,
          color: AppColors.brandGray,
          letterSpacing: 0.6),
    );
  }
}

class _ToggleChip extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;
  const _ToggleChip(
      {required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        decoration: BoxDecoration(
          color: selected
              ? AppColors.primary
              : const Color(0xFFF9FAFB),
          borderRadius: BorderRadius.circular(10),
          border: Border.all(
            color: selected
                ? AppColors.primary
                : const Color(0xFFE5E7EB),
          ),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: selected ? Colors.white : AppColors.brandGray,
          ),
        ),
      ),
    );
  }
}

class _Field extends StatelessWidget {
  final TextEditingController controller;
  final String label;
  final bool required;
  final TextInputType? keyboardType;
  final String? hintText;

  const _Field({
    required this.controller,
    required this.label,
    this.required = false,
    this.keyboardType,
    this.hintText,
  });

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      keyboardType: keyboardType,
      decoration: InputDecoration(
        labelText: required ? '$label *' : label,
        hintText: hintText,
        border:
            OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
      ),
      validator: required
          ? (v) => (v == null || v.trim().isEmpty) ? '$label is required' : null
          : null,
    );
  }
}

class _DatePicker extends StatelessWidget {
  final String label;
  final DateTime? value;
  final DateTime firstDate;
  final DateTime lastDate;
  final ValueChanged<DateTime?> onChanged;

  const _DatePicker({
    required this.label,
    required this.value,
    required this.firstDate,
    required this.lastDate,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () async {
        final picked = await showDatePicker(
          context: context,
          initialDate: value ?? DateTime.now(),
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
    );
  }
}

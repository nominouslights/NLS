import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/vehicle.dart';
import '../../domain/repositories/i_vehicle_repository.dart';
import '../providers/vehicle_form_provider.dart';
import '../providers/vehicles_provider.dart';

const _kVehicleTypes = [
  ('Sedan', 'Sedan'),
  ('SUV', 'SUV'),
  ('Minivan', 'Minivan'),
  ('PassengerVan', 'Passenger Van'),
  ('Minibus', 'Minibus'),
  ('Coach', 'Coach'),
  ('Accessible', 'Accessible'),
];

const _kProvinces = [
  'AB', 'BC', 'MB', 'NB', 'NL', 'NS', 'NT', 'NU', 'ON', 'PE', 'QC', 'SK', 'YT',
];

class VehicleFormSheet extends ConsumerStatefulWidget {
  final Vehicle? vehicle; // null = create

  const VehicleFormSheet({super.key, this.vehicle});

  @override
  ConsumerState<VehicleFormSheet> createState() =>
      _VehicleFormSheetState();
}

class _VehicleFormSheetState extends ConsumerState<VehicleFormSheet>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
  final _formKey = GlobalKey<FormState>();

  // Tab 0 — Vehicle Details
  final _unitCodeCtrl = TextEditingController();
  final _vinCtrl = TextEditingController();
  final _makeCtrl = TextEditingController();
  final _modelCtrl = TextEditingController();
  final _yearCtrl = TextEditingController();
  final _colorCtrl = TextEditingController();
  String _vehicleType = 'PassengerVan';
  String _province = 'ON';
  final _capacityCtrl = TextEditingController();
  final _odometerCtrl = TextEditingController();
  DateTime _acquisitionDate = DateTime.now();

  // Tab 1 — Compliance & Notes
  DateTime? _registrationExpiry;
  final _insuranceProviderCtrl = TextEditingController();
  final _policyNumberCtrl = TextEditingController();
  DateTime? _insuranceExpiry;
  final _notesCtrl = TextEditingController();
  bool _isActive = true;

  bool get _isEditing => widget.vehicle != null;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _tabController.addListener(() {
      if (mounted) setState(() {});
    });
    if (_isEditing) _populate(widget.vehicle!);
  }

  void _populate(Vehicle v) {
    _unitCodeCtrl.text = v.unitCode;
    _vinCtrl.text = v.vin;
    _makeCtrl.text = v.make;
    _modelCtrl.text = v.model;
    _yearCtrl.text = v.year.toString();
    _colorCtrl.text = v.color;
    _vehicleType = v.vehicleType;
    _province = v.province;
    _capacityCtrl.text = v.passengerCapacity.toString();
    _odometerCtrl.text = v.currentOdometerKm.toString();
    _acquisitionDate = v.acquisitionDate;
    _registrationExpiry = v.registrationExpiry;
    _insuranceProviderCtrl.text = v.insuranceProvider ?? '';
    _policyNumberCtrl.text = v.insurancePolicyNumber ?? '';
    _insuranceExpiry = v.insuranceExpiry;
    _notesCtrl.text = v.notes ?? '';
    _isActive = v.isActive;
  }

  @override
  void dispose() {
    _tabController.dispose();
    for (final c in [
      _unitCodeCtrl, _vinCtrl, _makeCtrl, _modelCtrl, _yearCtrl,
      _colorCtrl, _capacityCtrl, _odometerCtrl, _insuranceProviderCtrl,
      _policyNumberCtrl, _notesCtrl,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
            content: Text('Please correct the highlighted fields.')),
      );
      return;
    }

    final params = CreateVehicleParams(
      unitCode: _unitCodeCtrl.text.trim().toUpperCase(),
      vin: _vinCtrl.text.trim().toUpperCase(),
      make: _makeCtrl.text.trim(),
      model: _modelCtrl.text.trim(),
      year: int.tryParse(_yearCtrl.text.trim()) ?? DateTime.now().year,
      color: _colorCtrl.text.trim(),
      licensePlate: '', // updated below for UpdateVehicleParams
      province: _province,
      vehicleType: _vehicleType,
      passengerCapacity: int.tryParse(_capacityCtrl.text.trim()) ?? 1,
      currentOdometerKm: int.tryParse(_odometerCtrl.text.trim()) ?? 0,
      acquisitionDate: _acquisitionDate,
      registrationExpiry: _registrationExpiry,
      insuranceProvider: _insuranceProviderCtrl.text.trim().isNotEmpty
          ? _insuranceProviderCtrl.text.trim()
          : null,
      insurancePolicyNumber: _policyNumberCtrl.text.trim().isNotEmpty
          ? _policyNumberCtrl.text.trim()
          : null,
      insuranceExpiry: _insuranceExpiry,
      notes: _notesCtrl.text.trim().isNotEmpty
          ? _notesCtrl.text.trim()
          : null,
    );

    try {
      if (_isEditing) {
        final updateParams = UpdateVehicleParams(
          unitCode: params.unitCode,
          vin: params.vin,
          make: params.make,
          model: params.model,
          year: params.year,
          color: params.color,
          licensePlate: widget.vehicle!.licensePlate,
          province: params.province,
          vehicleType: params.vehicleType,
          passengerCapacity: params.passengerCapacity,
          currentOdometerKm: params.currentOdometerKm,
          acquisitionDate: params.acquisitionDate,
          registrationExpiry: params.registrationExpiry,
          insuranceProvider: params.insuranceProvider,
          insurancePolicyNumber: params.insurancePolicyNumber,
          insuranceExpiry: params.insuranceExpiry,
          notes: params.notes,
          isActive: _isActive,
        );
        await ref
            .read(vehicleFormProvider.notifier)
            .updateVehicle(widget.vehicle!.id, updateParams);
      } else {
        await ref.read(vehicleFormProvider.notifier).createVehicle(params);
      }
      if (mounted) {
        ref.invalidate(vehiclesProvider);
        Navigator.of(context).pop(true);
      }
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
    final isLoading = ref.watch(vehicleFormProvider).isLoading;
    final keyboardHeight = MediaQuery.of(context).viewInsets.bottom;
    final safeBottom = MediaQuery.of(context).padding.bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: keyboardHeight),
      child: Container(
        height: MediaQuery.of(context).size.height * 0.9,
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
                    _isEditing ? 'Edit Vehicle' : 'New Vehicle',
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
            TabBar(
              controller: _tabController,
              labelColor: AppColors.primary,
              unselectedLabelColor: AppColors.brandGray,
              indicatorColor: AppColors.primary,
              tabs: const [
                Tab(text: 'Vehicle Details'),
                Tab(text: 'Compliance & Notes'),
              ],
            ),
            const Divider(height: 1),
            Expanded(
              child: Form(
                key: _formKey,
                child: IndexedStack(
                  index: _tabController.index,
                  children: [
                    _VehicleDetailsTab(
                      unitCodeCtrl: _unitCodeCtrl,
                      vinCtrl: _vinCtrl,
                      makeCtrl: _makeCtrl,
                      modelCtrl: _modelCtrl,
                      yearCtrl: _yearCtrl,
                      colorCtrl: _colorCtrl,
                      vehicleType: _vehicleType,
                      province: _province,
                      capacityCtrl: _capacityCtrl,
                      odometerCtrl: _odometerCtrl,
                      acquisitionDate: _acquisitionDate,
                      onVehicleTypeChanged: (v) =>
                          setState(() => _vehicleType = v!),
                      onProvinceChanged: (v) =>
                          setState(() => _province = v!),
                      onAcquisitionDateChanged: (d) =>
                          setState(() => _acquisitionDate = d),
                    ),
                    _ComplianceTab(
                      registrationExpiry: _registrationExpiry,
                      insuranceProviderCtrl: _insuranceProviderCtrl,
                      policyNumberCtrl: _policyNumberCtrl,
                      insuranceExpiry: _insuranceExpiry,
                      notesCtrl: _notesCtrl,
                      isActive: _isActive,
                      isEditing: _isEditing,
                      onRegistrationExpiryChanged: (d) =>
                          setState(() => _registrationExpiry = d),
                      onInsuranceExpiryChanged: (d) =>
                          setState(() => _insuranceExpiry = d),
                      onActiveChanged: (v) =>
                          setState(() => _isActive = v),
                    ),
                  ],
                ),
              ),
            ),
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
                                  strokeWidth: 2, color: Colors.white))
                          : Text(_isEditing
                              ? 'Save Changes'
                              : 'Create Vehicle'),
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

// ── Tab: Vehicle Details ──────────────────────────────────────────────────────

class _VehicleDetailsTab extends StatelessWidget {
  final TextEditingController unitCodeCtrl;
  final TextEditingController vinCtrl;
  final TextEditingController makeCtrl;
  final TextEditingController modelCtrl;
  final TextEditingController yearCtrl;
  final TextEditingController colorCtrl;
  final String vehicleType;
  final String province;
  final TextEditingController capacityCtrl;
  final TextEditingController odometerCtrl;
  final DateTime acquisitionDate;
  final ValueChanged<String?> onVehicleTypeChanged;
  final ValueChanged<String?> onProvinceChanged;
  final ValueChanged<DateTime> onAcquisitionDateChanged;

  const _VehicleDetailsTab({
    required this.unitCodeCtrl,
    required this.vinCtrl,
    required this.makeCtrl,
    required this.modelCtrl,
    required this.yearCtrl,
    required this.colorCtrl,
    required this.vehicleType,
    required this.province,
    required this.capacityCtrl,
    required this.odometerCtrl,
    required this.acquisitionDate,
    required this.onVehicleTypeChanged,
    required this.onProvinceChanged,
    required this.onAcquisitionDateChanged,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        // Unit Code + VIN
        Row(
          children: [
            Expanded(
              child: _FormField(
                controller: unitCodeCtrl,
                label: 'Unit Code',
                required: true,
                hintText: 'e.g. NL-001',
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _FormField(
                controller: vinCtrl,
                label: 'VIN',
                required: true,
                hintText: '17-character VIN',
                textCapitalization: TextCapitalization.characters,
              ),
            ),
          ],
        ),
        const SizedBox(height: 14),
        // Make + Model
        Row(
          children: [
            Expanded(
              child: _FormField(
                  controller: makeCtrl, label: 'Make', required: true),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _FormField(
                  controller: modelCtrl, label: 'Model', required: true),
            ),
          ],
        ),
        const SizedBox(height: 14),
        // Year + Color
        Row(
          children: [
            Expanded(
              child: _FormField(
                controller: yearCtrl,
                label: 'Year',
                required: true,
                keyboardType: TextInputType.number,
                validator: (v) {
                  if (v == null || v.isEmpty) return 'Required';
                  final y = int.tryParse(v);
                  if (y == null || y < 1990 || y > 2040) return 'Invalid';
                  return null;
                },
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _FormField(
                  controller: colorCtrl, label: 'Color', required: true),
            ),
          ],
        ),
        const SizedBox(height: 14),
        // Vehicle Type
        DropdownButtonFormField<String>(
          value: vehicleType,
          decoration: InputDecoration(
            labelText: 'Vehicle Type *',
            border:
                OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
          ),
          items: _kVehicleTypes
              .map((e) =>
                  DropdownMenuItem(value: e.$1, child: Text(e.$2)))
              .toList(),
          onChanged: onVehicleTypeChanged,
        ),
        const SizedBox(height: 14),
        // Province
        DropdownButtonFormField<String>(
          value: province,
          decoration: InputDecoration(
            labelText: 'Province/Territory *',
            border:
                OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
          ),
          items: _kProvinces
              .map((p) => DropdownMenuItem(value: p, child: Text(p)))
              .toList(),
          onChanged: onProvinceChanged,
        ),
        const SizedBox(height: 14),
        // Capacity + Odometer
        Row(
          children: [
            Expanded(
              child: _FormField(
                controller: capacityCtrl,
                label: 'Passenger Capacity',
                required: true,
                keyboardType: TextInputType.number,
                validator: (v) {
                  if (v == null || v.isEmpty) return 'Required';
                  final n = int.tryParse(v);
                  if (n == null || n < 1) return 'Invalid';
                  return null;
                },
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _FormField(
                controller: odometerCtrl,
                label: 'Odometer (km)',
                required: true,
                keyboardType: TextInputType.number,
                validator: (v) {
                  if (v == null || v.isEmpty) return 'Required';
                  if (int.tryParse(v) == null) return 'Invalid';
                  return null;
                },
              ),
            ),
          ],
        ),
        const SizedBox(height: 14),
        // Acquisition Date
        InkWell(
          onTap: () async {
            final picked = await showDatePicker(
              context: context,
              initialDate: acquisitionDate,
              firstDate: DateTime(2000),
              lastDate: DateTime.now(),
            );
            if (picked != null) onAcquisitionDateChanged(picked);
          },
          borderRadius: BorderRadius.circular(12),
          child: InputDecorator(
            decoration: InputDecoration(
              labelText: 'Acquisition Date *',
              suffixIcon:
                  const Icon(Icons.calendar_today_outlined, size: 18),
              border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12)),
            ),
            child: Text(
              DateFormat('MMM d, yyyy').format(acquisitionDate),
              style: const TextStyle(
                  fontSize: 14, color: Color(0xFF111827)),
            ),
          ),
        ),
      ],
    );
  }
}

// ── Tab: Compliance & Notes ───────────────────────────────────────────────────

class _ComplianceTab extends StatelessWidget {
  final DateTime? registrationExpiry;
  final TextEditingController insuranceProviderCtrl;
  final TextEditingController policyNumberCtrl;
  final DateTime? insuranceExpiry;
  final TextEditingController notesCtrl;
  final bool isActive;
  final bool isEditing;
  final ValueChanged<DateTime?> onRegistrationExpiryChanged;
  final ValueChanged<DateTime?> onInsuranceExpiryChanged;
  final ValueChanged<bool> onActiveChanged;

  const _ComplianceTab({
    required this.registrationExpiry,
    required this.insuranceProviderCtrl,
    required this.policyNumberCtrl,
    required this.insuranceExpiry,
    required this.notesCtrl,
    required this.isActive,
    required this.isEditing,
    required this.onRegistrationExpiryChanged,
    required this.onInsuranceExpiryChanged,
    required this.onActiveChanged,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        // Registration expiry
        _OptionalDatePicker(
          label: 'Registration Expiry',
          value: registrationExpiry,
          onChanged: onRegistrationExpiryChanged,
        ),
        const SizedBox(height: 14),
        // Insurance provider + policy
        _FormField(
          controller: insuranceProviderCtrl,
          label: 'Insurance Provider',
        ),
        const SizedBox(height: 14),
        _FormField(
          controller: policyNumberCtrl,
          label: 'Policy Number',
        ),
        const SizedBox(height: 14),
        // Insurance expiry
        _OptionalDatePicker(
          label: 'Insurance Expiry',
          value: insuranceExpiry,
          onChanged: onInsuranceExpiryChanged,
        ),
        const SizedBox(height: 14),
        // Notes
        TextFormField(
          controller: notesCtrl,
          maxLines: 4,
          decoration: InputDecoration(
            labelText: 'Notes (optional)',
            border:
                OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
            alignLabelWithHint: true,
          ),
        ),
        if (isEditing) ...[
          const SizedBox(height: 20),
          Row(
            children: [
              const Expanded(
                child: Text('Active',
                    style: TextStyle(
                        fontSize: 14, fontWeight: FontWeight.w500)),
              ),
              Switch(
                value: isActive,
                onChanged: onActiveChanged,
                activeColor: AppColors.primary,
              ),
            ],
          ),
        ],
      ],
    );
  }
}

class _OptionalDatePicker extends StatelessWidget {
  final String label;
  final DateTime? value;
  final ValueChanged<DateTime?> onChanged;

  const _OptionalDatePicker({
    required this.label,
    required this.value,
    required this.onChanged,
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
                initialDate: value ??
                    DateTime.now().add(const Duration(days: 365)),
                firstDate: DateTime(2000),
                lastDate: DateTime(2040),
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
                    : 'Not set',
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
        if (value != null) ...[
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

// ── Shared ────────────────────────────────────────────────────────────────────

class _FormField extends StatelessWidget {
  final TextEditingController controller;
  final String label;
  final bool required;
  final TextInputType? keyboardType;
  final String? hintText;
  final String? Function(String?)? validator;
  final TextCapitalization textCapitalization;

  const _FormField({
    required this.controller,
    required this.label,
    this.required = false,
    this.keyboardType,
    this.hintText,
    this.validator,
    this.textCapitalization = TextCapitalization.none,
  });

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      keyboardType: keyboardType,
      textCapitalization: textCapitalization,
      decoration: InputDecoration(
        labelText: required ? '$label *' : label,
        hintText: hintText,
        border:
            OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
      ),
      validator: validator ??
          (required
              ? (v) => (v == null || v.trim().isEmpty)
                  ? '$label is required'
                  : null
              : null),
    );
  }
}

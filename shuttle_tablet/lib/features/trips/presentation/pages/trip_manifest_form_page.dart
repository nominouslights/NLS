import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../clients/presentation/providers/clients_provider.dart';
import '../../../drivers/presentation/providers/drivers_provider.dart';
import '../../../locations/domain/entities/saved_location.dart';
import '../../../locations/presentation/providers/locations_provider.dart';
import '../../../vehicles/presentation/providers/vehicles_provider.dart';
import '../../domain/entities/trip.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../providers/trip_form_provider.dart';
import '../providers/trips_provider.dart';

class TripManifestFormPage extends ConsumerStatefulWidget {
  final Trip? trip; // null = create, non-null = edit
  const TripManifestFormPage({super.key, this.trip});

  @override
  ConsumerState<TripManifestFormPage> createState() =>
      _TripManifestFormPageState();
}

class _TripManifestFormPageState extends ConsumerState<TripManifestFormPage> {
  final _pageController = PageController();
  int _currentStep = 0;
  bool _isSaving = false;

  // Step 1 state
  final _formKey1 = GlobalKey<FormState>();
  String? _selectedClientId;
  String? _selectedVehicleId;
  final _poController = TextEditingController();
  final _notesController = TextEditingController();
  DateTime _scheduledAt = DateTime.now().add(const Duration(hours: 1));
  final List<_StopEntry> _stops = [
    _StopEntry(sequenceOrder: 1, label: 'Pickup'),
    _StopEntry(sequenceOrder: 2, label: 'Drop-off'),
  ];

  // Step 2 state
  final _formKey2 = GlobalKey<FormState>();
  String? _selectedDriverId;

  @override
  void initState() {
    super.initState();
    if (widget.trip != null) {
      final t = widget.trip!;
      _selectedClientId = t.clientId;
      _selectedVehicleId = t.vehicleId;
      _poController.text = t.purchaseOrderNumber ?? '';
      _notesController.text = t.notes ?? '';
      _scheduledAt = t.scheduledAt;
      _stops.clear();
      for (final s in t.stops) {
        _stops.add(_StopEntry(
          sequenceOrder: s.sequenceOrder,
          label: s.sequenceOrder == 1
              ? 'Pickup'
              : s.sequenceOrder == t.stops.length
                  ? 'Drop-off'
                  : 'Stop ${s.sequenceOrder}',
          location: s.locationName,
          address: s.address ?? '',
        ));
      }
      _selectedDriverId = t.driverId;
    }
  }

  @override
  void dispose() {
    _pageController.dispose();
    _poController.dispose();
    _notesController.dispose();
    for (final s in _stops) {
      s.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final vehiclesAsync = ref.watch(vehiclesProvider);
    final selectedVehicle = vehiclesAsync.value
        ?.where((v) => v.id == _selectedVehicleId)
        .firstOrNull;
    final vehicleLabel = selectedVehicle != null
        ? '${selectedVehicle.unitCode} — ${selectedVehicle.make} ${selectedVehicle.model}'
        : '';

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: Text(
          widget.trip == null ? 'New Trip' : 'Edit Trip',
          style: const TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w700,
            color: AppColors.textPrimary,
          ),
        ),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(4),
          child: _StepIndicator(current: _currentStep, total: 2),
        ),
      ),
      body: PageView(
        controller: _pageController,
        physics: const NeverScrollableScrollPhysics(),
        children: [
          _Step1(
            formKey: _formKey1,
            selectedClientId: _selectedClientId,
            onClientChanged: (v) => setState(() => _selectedClientId = v),
            selectedVehicleId: _selectedVehicleId,
            onVehicleChanged: (v) => setState(() => _selectedVehicleId = v),
            poController: _poController,
            notesController: _notesController,
            scheduledAt: _scheduledAt,
            onScheduledChanged: (dt) => setState(() => _scheduledAt = dt),
            stops: _stops,
            onAddStop: _addStop,
            onRemoveStop: _removeStop,
          ),
          _Step2(
            formKey: _formKey2,
            stops: _stops,
            scheduledAt: _scheduledAt,
            vehicleLabel: vehicleLabel,
            selectedDriverId: _selectedDriverId,
            onDriverChanged: (v) => setState(() => _selectedDriverId = v),
          ),
        ],
      ),
      bottomNavigationBar: _BottomBar(
        currentStep: _currentStep,
        isSaving: _isSaving,
        onCancel: _currentStep == 0 ? () => Navigator.of(context).pop() : null,
        onBack: _currentStep > 0 ? _goBack : null,
        onNext: _currentStep == 0 ? _goNext : null,
        onSaveDraft: _currentStep == 1 ? _saveDraft : null,
        onCreateDispatch: _currentStep == 1 ? _createAndDispatch : null,
      ),
    );
  }

  void _addStop() {
    setState(() {
      final next = _stops.length + 1;
      // Insert before last (drop-off)
      _stops.insert(
        _stops.length - 1,
        _StopEntry(
          sequenceOrder: next,
          label: 'Stop $next',
        ),
      );
      // Renumber drop-off
      _stops.last.sequenceOrder = _stops.length;
    });
  }

  void _removeStop(int index) {
    if (_stops.length <= 2) return; // must keep pickup + dropoff
    setState(() {
      _stops[index].dispose();
      _stops.removeAt(index);
      for (var i = 0; i < _stops.length; i++) {
        _stops[i].sequenceOrder = i + 1;
        if (i == 0) _stops[i].label = 'Pickup';
        if (i == _stops.length - 1) _stops[i].label = 'Drop-off';
      }
    });
  }

  void _goNext() {
    if (!_formKey1.currentState!.validate()) return;
    if (_selectedClientId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select a client')),
      );
      return;
    }
    if (_selectedVehicleId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select a vehicle')),
      );
      return;
    }
    if (_stops.any((s) => s.locationController.text.trim().isEmpty)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('All stops must have a location')),
      );
      return;
    }
    setState(() => _currentStep = 1);
    _pageController.nextPage(
        duration: const Duration(milliseconds: 300), curve: Curves.easeInOut);
  }

  void _goBack() {
    setState(() => _currentStep = 0);
    _pageController.previousPage(
        duration: const Duration(milliseconds: 300), curve: Curves.easeInOut);
  }

  List<StopParams> get _stopParams => _stops
      .map((s) => StopParams(
            sequenceOrder: s.sequenceOrder,
            locationName: s.locationController.text.trim(),
            address: s.addressController.text.trim().isEmpty
                ? null
                : s.addressController.text.trim(),
          ))
      .toList();

  String? get _vehicleType => ref
      .read(vehiclesProvider)
      .value
      ?.where((v) => v.id == _selectedVehicleId)
      .firstOrNull
      ?.vehicleType;

  Future<void> _saveDraft() async {
    setState(() => _isSaving = true);
    try {
      if (widget.trip == null) {
        final params = CreateTripParams(
          clientId: _selectedClientId!,
          vehicleId: _selectedVehicleId!,
          purchaseOrderNumber: _poController.text.trim().isEmpty
              ? null
              : _poController.text.trim(),
          vehicleType: _vehicleType,
          scheduledAt: _scheduledAt,
          notes: _notesController.text.trim().isEmpty
              ? null
              : _notesController.text.trim(),
          stops: _stopParams,
        );
        final id = await ref.read(tripFormProvider.notifier).createTrip(params);
        // Optionally assign driver if selected
        if (_selectedDriverId != null) {
          await ref.read(tripFormProvider.notifier).assignDriver(
                id,
                AssignDriverParams(
                  driverId: _selectedDriverId!,
                  vehicleType: _vehicleType,
                ),
              );
        }
      } else {
        final params = UpdateTripParams(
          vehicleId: _selectedVehicleId!,
          purchaseOrderNumber: _poController.text.trim().isEmpty
              ? null
              : _poController.text.trim(),
          vehicleType: _vehicleType,
          scheduledAt: _scheduledAt,
          notes: _notesController.text.trim().isEmpty
              ? null
              : _notesController.text.trim(),
          stops: _stopParams,
        );
        await ref
            .read(tripFormProvider.notifier)
            .updateTrip(widget.trip!.id, params);
      }
      ref.invalidate(tripsProvider);
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error: $e'), backgroundColor: AppColors.danger),
        );
      }
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }

  Future<void> _createAndDispatch() async {
    if (_selectedDriverId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Select a driver to dispatch')),
      );
      return;
    }
    setState(() => _isSaving = true);
    try {
      final params = CreateTripParams(
        clientId: _selectedClientId!,
        vehicleId: _selectedVehicleId!,
        purchaseOrderNumber: _poController.text.trim().isEmpty
            ? null
            : _poController.text.trim(),
        vehicleType: _vehicleType,
        scheduledAt: _scheduledAt,
        notes: _notesController.text.trim().isEmpty
            ? null
            : _notesController.text.trim(),
        stops: _stopParams,
      );
      final id = await ref.read(tripFormProvider.notifier).createTrip(params);

      await ref.read(tripFormProvider.notifier).assignDriver(
            id,
            AssignDriverParams(
              driverId: _selectedDriverId!,
              vehicleType: _vehicleType,
            ),
          );

      await ref.read(tripFormProvider.notifier).dispatchTrip(id);

      ref.invalidate(tripsProvider);
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text('Error: $e'), backgroundColor: AppColors.danger),
        );
      }
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }
}

// ── Step 1 ────────────────────────────────────────────────────────────────────

class _Step1 extends ConsumerWidget {
  final GlobalKey<FormState> formKey;
  final String? selectedClientId;
  final ValueChanged<String?> onClientChanged;
  final String? selectedVehicleId;
  final ValueChanged<String?> onVehicleChanged;
  final TextEditingController poController;
  final TextEditingController notesController;
  final DateTime scheduledAt;
  final ValueChanged<DateTime> onScheduledChanged;
  final List<_StopEntry> stops;
  final VoidCallback onAddStop;
  final void Function(int) onRemoveStop;

  const _Step1({
    required this.formKey,
    required this.selectedClientId,
    required this.onClientChanged,
    required this.selectedVehicleId,
    required this.onVehicleChanged,
    required this.poController,
    required this.notesController,
    required this.scheduledAt,
    required this.onScheduledChanged,
    required this.stops,
    required this.onAddStop,
    required this.onRemoveStop,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final clientsAsync = ref.watch(clientsProvider);
    final vehiclesAsync = ref.watch(vehiclesProvider);

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _Label('Client *'),
            const SizedBox(height: 6),
            clientsAsync.when(
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => Text('Failed to load clients: $e',
                  style: const TextStyle(color: AppColors.danger)),
              data: (clients) => DropdownButtonFormField<String>(
                value: selectedClientId,
                decoration: _inputDecoration('Select client'),
                isExpanded: true,
                items: clients
                    .map((c) => DropdownMenuItem(
                          value: c.id,
                          child: Text(c.businessName),
                        ))
                    .toList(),
                onChanged: onClientChanged,
                validator: (v) => v == null ? 'Required' : null,
              ),
            ),
            const SizedBox(height: 16),
            _Label('Vehicle *'),
            const SizedBox(height: 6),
            vehiclesAsync.when(
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => Text('Failed to load vehicles: $e',
                  style: const TextStyle(color: AppColors.danger)),
              data: (vehicles) => DropdownButtonFormField<String>(
                value: selectedVehicleId,
                decoration: _inputDecoration('Select vehicle'),
                isExpanded: true,
                items: vehicles
                    .where((v) => v.isActive)
                    .map((v) => DropdownMenuItem(
                          value: v.id,
                          child: Text(
                              '${v.unitCode} — ${v.make} ${v.model}'),
                        ))
                    .toList(),
                onChanged: onVehicleChanged,
                validator: (v) => v == null ? 'Required' : null,
              ),
            ),
            const SizedBox(height: 16),
            _Label('Purchase Order #'),
            const SizedBox(height: 6),
            TextFormField(
              controller: poController,
              decoration: _inputDecoration('e.g. PO-2024-001'),
            ),
            const SizedBox(height: 16),
            _Label('Scheduled Date & Time *'),
            const SizedBox(height: 6),
            InkWell(
              onTap: () => _pickDateTime(context),
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 13),
                decoration: BoxDecoration(
                  color: Colors.white,
                  border: Border.all(color: const Color(0xFFD1D5DB)),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.calendar_today_rounded,
                        size: 18, color: AppColors.brandGray),
                    const SizedBox(width: 10),
                    Text(
                      DateFormat('MMM d, yyyy · h:mm a')
                          .format(scheduledAt.toLocal()),
                      style: const TextStyle(fontSize: 14),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 20),
            // Stops
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text(
                  'Route Stops',
                  style: TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                TextButton.icon(
                  onPressed: onAddStop,
                  icon: const Icon(Icons.add_rounded, size: 16),
                  label: const Text('Add Stop'),
                ),
              ],
            ),
            const SizedBox(height: 8),
            ...stops.asMap().entries.map((entry) {
              final i = entry.key;
              final stop = entry.value;
              return Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: _StopRow(
                  stop: stop,
                  index: i,
                  canRemove: i > 0 && i < stops.length - 1,
                  onRemove: () => onRemoveStop(i),
                ),
              );
            }),
            const SizedBox(height: 16),
            _Label('Notes'),
            const SizedBox(height: 6),
            TextFormField(
              controller: notesController,
              maxLines: 3,
              decoration: _inputDecoration('Any special instructions…'),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _pickDateTime(BuildContext context) async {
    final date = await showDatePicker(
      context: context,
      initialDate: scheduledAt,
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );
    if (date == null || !context.mounted) return;

    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(scheduledAt),
    );
    if (time == null) return;

    onScheduledChanged(DateTime(
      date.year,
      date.month,
      date.day,
      time.hour,
      time.minute,
    ));
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

class _StopRow extends ConsumerWidget {
  final _StopEntry stop;
  final int index;
  final bool canRemove;
  final VoidCallback onRemove;

  const _StopRow({
    required this.stop,
    required this.index,
    required this.canRemove,
    required this.onRemove,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 28,
                height: 28,
                decoration: BoxDecoration(
                  color: stop.label == 'Pickup'
                      ? AppColors.primary
                      : stop.label == 'Drop-off'
                          ? const Color(0xFF059669)
                          : AppColors.brandGray,
                  shape: BoxShape.circle,
                ),
                child: Center(
                  child: Text(
                    '${stop.sequenceOrder}',
                    style: const TextStyle(
                        color: Colors.white,
                        fontSize: 12,
                        fontWeight: FontWeight.w700),
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Text(
                stop.label,
                style: const TextStyle(
                    fontSize: 13, fontWeight: FontWeight.w700),
              ),
              const Spacer(),
              IconButton(
                onPressed: () => _pickSavedLocation(context, ref),
                icon: const Icon(Icons.bookmark_add_outlined,
                    size: 18, color: AppColors.primary),
                tooltip: 'Pick saved location',
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(),
              ),
              if (canRemove) ...[
                const SizedBox(width: 4),
                IconButton(
                  onPressed: onRemove,
                  icon: const Icon(Icons.remove_circle_outline_rounded,
                      size: 20, color: AppColors.danger),
                  padding: EdgeInsets.zero,
                  constraints: const BoxConstraints(),
                ),
              ],
            ],
          ),
          const SizedBox(height: 10),
          TextFormField(
            controller: stop.locationController,
            decoration: const InputDecoration(
              hintText: 'Location name *',
              isDense: true,
              filled: true,
              fillColor: Color(0xFFF9FAFB),
              border: OutlineInputBorder(),
              contentPadding:
                  EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            ),
            validator: (v) =>
                v == null || v.trim().isEmpty ? 'Required' : null,
          ),
          const SizedBox(height: 8),
          TextFormField(
            controller: stop.addressController,
            decoration: const InputDecoration(
              hintText: 'Address (optional)',
              isDense: true,
              filled: true,
              fillColor: Color(0xFFF9FAFB),
              border: OutlineInputBorder(),
              contentPadding:
                  EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _pickSavedLocation(BuildContext context, WidgetRef ref) async {
    final result = await showModalBottomSheet<SavedLocation>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => const _LocationPickerSheet(),
    );
    if (result != null) {
      stop.locationController.text = result.name;
      stop.addressController.text = result.address ?? '';
    }
  }
}

// ── Location Picker Sheet ─────────────────────────────────────────────────────

class _LocationPickerSheet extends ConsumerStatefulWidget {
  const _LocationPickerSheet();

  @override
  ConsumerState<_LocationPickerSheet> createState() =>
      _LocationPickerSheetState();
}

class _LocationPickerSheetState extends ConsumerState<_LocationPickerSheet> {
  String _search = '';

  @override
  Widget build(BuildContext context) {
    final locationsAsync = ref.watch(locationsProvider);

    return DraggableScrollableSheet(
      initialChildSize: 0.6,
      minChildSize: 0.4,
      maxChildSize: 0.9,
      expand: false,
      builder: (_, scrollController) => Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
        ),
        child: Column(
          children: [
            const SizedBox(height: 8),
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
            const SizedBox(height: 12),
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 20),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Pick a Saved Location',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827),
                  ),
                ),
              ),
            ),
            const SizedBox(height: 12),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: TextField(
                autofocus: true,
                decoration: InputDecoration(
                  hintText: 'Search locations…',
                  prefixIcon: const Icon(Icons.search_rounded, size: 18),
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
                  contentPadding: const EdgeInsets.symmetric(vertical: 10),
                ),
                onChanged: (v) => setState(() => _search = v),
              ),
            ),
            const SizedBox(height: 8),
            Expanded(
              child: locationsAsync.when(
                loading: () =>
                    const Center(child: CircularProgressIndicator()),
                error: (e, _) => Center(
                  child: Text('Failed to load: $e',
                      style: const TextStyle(color: AppColors.danger)),
                ),
                data: (locations) {
                  final filtered = _search.isEmpty
                      ? locations
                      : locations
                          .where((l) =>
                              l.name
                                  .toLowerCase()
                                  .contains(_search.toLowerCase()) ||
                              (l.address
                                      ?.toLowerCase()
                                      .contains(_search.toLowerCase()) ??
                                  false))
                          .toList();

                  if (locations.isEmpty) {
                    return const Center(
                      child: Padding(
                        padding: EdgeInsets.all(24),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(Icons.bookmark_border_rounded,
                                size: 40, color: AppColors.brandGray),
                            SizedBox(height: 8),
                            Text(
                              'No saved locations yet',
                              style: TextStyle(
                                  fontWeight: FontWeight.w600,
                                  color: AppColors.brandGray),
                            ),
                            SizedBox(height: 4),
                            Text(
                              'Add locations from the Saved Locations page.',
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                  fontSize: 12, color: AppColors.brandGray),
                            ),
                          ],
                        ),
                      ),
                    );
                  }

                  if (filtered.isEmpty) {
                    return const Center(
                      child: Text('No locations match your search.',
                          style: TextStyle(color: AppColors.brandGray)),
                    );
                  }

                  return ListView.builder(
                    controller: scrollController,
                    padding: const EdgeInsets.only(bottom: 16),
                    itemCount: filtered.length,
                    itemBuilder: (_, i) {
                      final loc = filtered[i];
                      return ListTile(
                        leading: const Icon(Icons.place_rounded,
                            color: AppColors.primary, size: 20),
                        title: Text(
                          loc.name,
                          style: const TextStyle(
                            fontWeight: FontWeight.w600,
                            fontSize: 14,
                          ),
                        ),
                        subtitle: loc.address != null
                            ? Text(
                                loc.address!,
                                style: const TextStyle(
                                    fontSize: 12,
                                    color: AppColors.brandGray),
                              )
                            : null,
                        trailing: loc.hasCoordinates
                            ? Container(
                                padding: const EdgeInsets.symmetric(
                                    horizontal: 6, vertical: 2),
                                decoration: BoxDecoration(
                                  color: AppColors.secondary
                                      .withValues(alpha: 0.1),
                                  borderRadius: BorderRadius.circular(4),
                                ),
                                child: Text(
                                  '${loc.latitude!.toStringAsFixed(3)}, ${loc.longitude!.toStringAsFixed(3)}',
                                  style: const TextStyle(
                                    fontSize: 10,
                                    color: AppColors.secondary,
                                  ),
                                ),
                              )
                            : null,
                        onTap: () => Navigator.pop(context, loc),
                      );
                    },
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ── Step 2 ────────────────────────────────────────────────────────────────────

class _Step2 extends ConsumerWidget {
  final GlobalKey<FormState> formKey;
  final List<_StopEntry> stops;
  final DateTime scheduledAt;
  final String vehicleLabel;
  final String? selectedDriverId;
  final ValueChanged<String?> onDriverChanged;

  const _Step2({
    required this.formKey,
    required this.stops,
    required this.scheduledAt,
    required this.vehicleLabel,
    required this.selectedDriverId,
    required this.onDriverChanged,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final driversAsync = ref.watch(driversProvider);

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Trip summary card
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
                  const Text(
                    'Trip Summary',
                    style: TextStyle(
                        fontSize: 15, fontWeight: FontWeight.w700),
                  ),
                  const Divider(height: 16),
                  _SummaryRow('Scheduled', DateFormat('MMM d, yyyy · h:mm a')
                      .format(scheduledAt.toLocal())),
                  _SummaryRow('Stops', '${stops.length}'),
                  if (vehicleLabel.isNotEmpty)
                    _SummaryRow('Vehicle', vehicleLabel),
                  ...stops.map((s) => _SummaryRow(
                        s.label,
                        s.locationController.text.isEmpty
                            ? '—'
                            : s.locationController.text,
                      )),
                ],
              ),
            ),
            const SizedBox(height: 20),
            _Label('Assign Driver (optional)'),
            const SizedBox(height: 6),
            driversAsync.when(
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => Text('Failed to load drivers: $e',
                  style: const TextStyle(color: AppColors.danger)),
              data: (drivers) => DropdownButtonFormField<String>(
                value: selectedDriverId,
                decoration: _inputDecoration('Select driver'),
                isExpanded: true,
                items: [
                  const DropdownMenuItem(value: null, child: Text('No driver')),
                  ...drivers.map((d) => DropdownMenuItem(
                        value: d.id,
                        child: Text(d.fullName),
                      )),
                ],
                onChanged: onDriverChanged,
              ),
            ),
            const SizedBox(height: 8),
            const Text(
              'A driver must be assigned before dispatching.',
              style: TextStyle(fontSize: 12, color: AppColors.brandGray),
            ),
          ],
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

class _SummaryRow extends StatelessWidget {
  final String label;
  final String value;
  const _SummaryRow(this.label, this.value);

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 6),
        child: Row(
          children: [
            SizedBox(
              width: 110,
              child: Text(label,
                  style: const TextStyle(
                      fontSize: 13, color: AppColors.textSecondary)),
            ),
            Expanded(
              child: Text(value,
                  style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textPrimary)),
            ),
          ],
        ),
      );
}

// ── Bottom Navigation Bar ─────────────────────────────────────────────────────

class _BottomBar extends StatelessWidget {
  final int currentStep;
  final bool isSaving;
  final VoidCallback? onCancel;
  final VoidCallback? onBack;
  final VoidCallback? onNext;
  final VoidCallback? onSaveDraft;
  final VoidCallback? onCreateDispatch;

  const _BottomBar({
    required this.currentStep,
    required this.isSaving,
    this.onCancel,
    this.onBack,
    this.onNext,
    this.onSaveDraft,
    this.onCreateDispatch,
  });

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Container(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
        decoration: const BoxDecoration(
          color: Colors.white,
          border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
        ),
        child: Row(
          children: [
            if (onCancel != null)
              OutlinedButton(
                onPressed: isSaving ? null : onCancel,
                child: const Text('Cancel'),
              ),
            if (onBack != null)
              OutlinedButton.icon(
                onPressed: isSaving ? null : onBack,
                icon: const Icon(Icons.arrow_back_rounded, size: 16),
                label: const Text('Back'),
              ),
            const Spacer(),
            if (onNext != null)
              FilledButton.icon(
                onPressed: isSaving ? null : onNext,
                icon: const Icon(Icons.arrow_forward_rounded, size: 16),
                label: const Text('Review'),
                style:
                    FilledButton.styleFrom(backgroundColor: AppColors.primary),
              ),
            if (onSaveDraft != null) ...[
              OutlinedButton(
                onPressed: isSaving ? null : onSaveDraft,
                child: isSaving
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Save Draft'),
              ),
              const SizedBox(width: 10),
            ],
            if (onCreateDispatch != null)
              FilledButton.icon(
                onPressed: isSaving ? null : onCreateDispatch,
                icon: const Icon(Icons.send_rounded, size: 16),
                label: const Text('Create & Dispatch'),
                style:
                    FilledButton.styleFrom(backgroundColor: AppColors.primary),
              ),
          ],
        ),
      ),
    );
  }
}

// ── Step Indicator ────────────────────────────────────────────────────────────

class _StepIndicator extends StatelessWidget {
  final int current;
  final int total;
  const _StepIndicator({required this.current, required this.total});

  @override
  Widget build(BuildContext context) {
    return LinearProgressIndicator(
      value: (current + 1) / total,
      backgroundColor: const Color(0xFFE5E7EB),
      color: AppColors.primary,
      minHeight: 3,
    );
  }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

class _Label extends StatelessWidget {
  final String text;
  const _Label(this.text);

  @override
  Widget build(BuildContext context) => Text(
        text,
        style: const TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.w600,
          color: AppColors.textPrimary,
        ),
      );
}

// ── Stop Entry (mutable state holder) ────────────────────────────────────────

class _StopEntry {
  int sequenceOrder;
  String label;
  final TextEditingController locationController;
  final TextEditingController addressController;

  _StopEntry({
    required this.sequenceOrder,
    required this.label,
    String location = '',
    String address = '',
  })  : locationController = TextEditingController(text: location),
        addressController = TextEditingController(text: address);

  void dispose() {
    locationController.dispose();
    addressController.dispose();
  }
}

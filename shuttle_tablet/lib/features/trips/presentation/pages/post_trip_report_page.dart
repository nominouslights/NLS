import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/delay_entry.dart';
import '../../domain/entities/trip_post_report.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../providers/trip_form_provider.dart';
import '../providers/trips_provider.dart';

class PostTripReportPage extends ConsumerStatefulWidget {
  final String tripId;
  final DelayHandoff? delayHandoff;
  const PostTripReportPage({super.key, required this.tripId, this.delayHandoff});

  @override
  ConsumerState<PostTripReportPage> createState() =>
      _PostTripReportPageState();
}

class _PostTripReportPageState extends ConsumerState<PostTripReportPage> {
  final _formKey = GlobalKey<FormState>();
  final _odometerEndController = TextEditingController();
  final _fuelLitresController = TextEditingController();
  final _fuelCostController = TextEditingController();
  final _incidentDescController = TextEditingController();
  final _additionalNotesController = TextEditingController();

  bool _hasIncident = false;
  IncidentType? _incidentType;
  bool _isReadyToInvoice = false;
  bool _isSaving = false;

  @override
  void initState() {
    super.initState();
    final handoff = widget.delayHandoff;
    if (handoff != null) {
      _hasIncident = true;
      _incidentType = handoff.type;
      _incidentDescController.text = handoff.description;
    }
  }

  int? get _odometerStart {
    final tripAsync = ref.read(tripDetailProvider(widget.tripId));
    return tripAsync.valueOrNull?.preInspection?.odometerStart;
  }

  int? get _distanceKm {
    final end = int.tryParse(_odometerEndController.text);
    final start = _odometerStart;
    if (end == null || start == null) return null;
    final d = end - start;
    return d >= 0 ? d : null;
  }

  @override
  void dispose() {
    _odometerEndController.dispose();
    _fuelLitresController.dispose();
    _fuelCostController.dispose();
    _incidentDescController.dispose();
    _additionalNotesController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final tripAsync = ref.watch(tripDetailProvider(widget.tripId));
    final odometerStart =
        tripAsync.valueOrNull?.preInspection?.odometerStart;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          'Post-Trip Report',
          style: TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w700,
            color: AppColors.textPrimary,
          ),
        ),
      ),
      body: Form(
        key: _formKey,
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(20, 20, 20, 120),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Odometer section
              _Card(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const _CardTitle('Odometer', Icons.speed_rounded),
                    const SizedBox(height: 12),
                    if (odometerStart != null)
                      _InfoRow('Start', '$odometerStart km'),
                    const SizedBox(height: 10),
                    TextFormField(
                      controller: _odometerEndController,
                      keyboardType: TextInputType.number,
                      inputFormatters: [
                        FilteringTextInputFormatter.digitsOnly
                      ],
                      decoration: const InputDecoration(
                        labelText: 'Odometer End *',
                        suffixText: 'km',
                        isDense: true,
                        filled: true,
                        fillColor: Color(0xFFF9FAFB),
                        border: OutlineInputBorder(),
                        contentPadding: EdgeInsets.symmetric(
                            horizontal: 14, vertical: 12),
                      ),
                      onChanged: (_) => setState(() {}),
                      validator: (v) {
                        if (v == null || v.isEmpty) return 'Required';
                        if (int.tryParse(v) == null) return 'Enter a number';
                        if (odometerStart != null &&
                            (int.tryParse(v) ?? 0) < odometerStart) {
                          return 'Must be ≥ start ($odometerStart km)';
                        }
                        return null;
                      },
                    ),
                    if (_distanceKm != null) ...[
                      const SizedBox(height: 10),
                      Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 14, vertical: 10),
                        decoration: BoxDecoration(
                          color: const Color(0xFFEBF0FA),
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Row(
                          children: [
                            const Icon(Icons.route_rounded,
                                size: 16, color: AppColors.primary),
                            const SizedBox(width: 8),
                            Text(
                              'Distance: $_distanceKm km',
                              style: const TextStyle(
                                fontSize: 14,
                                fontWeight: FontWeight.w600,
                                color: AppColors.primary,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 16),

              // Fuel section
              _Card(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const _CardTitle('Fuel', Icons.local_gas_station_rounded),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: TextFormField(
                            controller: _fuelLitresController,
                            keyboardType: const TextInputType.numberWithOptions(
                                decimal: true),
                            decoration: const InputDecoration(
                              labelText: 'Litres Added',
                              suffixText: 'L',
                              isDense: true,
                              filled: true,
                              fillColor: Color(0xFFF9FAFB),
                              border: OutlineInputBorder(),
                              contentPadding: EdgeInsets.symmetric(
                                  horizontal: 14, vertical: 12),
                            ),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: TextFormField(
                            controller: _fuelCostController,
                            keyboardType: const TextInputType.numberWithOptions(
                                decimal: true),
                            decoration: const InputDecoration(
                              labelText: 'Cost',
                              prefixText: '\$',
                              isDense: true,
                              filled: true,
                              fillColor: Color(0xFFF9FAFB),
                              border: OutlineInputBorder(),
                              contentPadding: EdgeInsets.symmetric(
                                  horizontal: 14, vertical: 12),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),

              // Incident section
              _Card(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const _CardTitle(
                        'Incident Report', Icons.warning_amber_rounded),
                    const SizedBox(height: 8),
                    SwitchListTile(
                      contentPadding: EdgeInsets.zero,
                      title: const Text(
                        'Was there an incident?',
                        style: TextStyle(fontSize: 14),
                      ),
                      value: _hasIncident,
                      onChanged: (v) => setState(() {
                        _hasIncident = v;
                        if (!v) {
                          _incidentType = null;
                          _incidentDescController.clear();
                        }
                      }),
                      activeColor: AppColors.danger,
                    ),
                    if (_hasIncident) ...[
                      const SizedBox(height: 10),
                      DropdownButtonFormField<IncidentType>(
                        value: _incidentType,
                        decoration: const InputDecoration(
                          labelText: 'Incident Type *',
                          isDense: true,
                          filled: true,
                          fillColor: Color(0xFFF9FAFB),
                          border: OutlineInputBorder(),
                          contentPadding: EdgeInsets.symmetric(
                              horizontal: 14, vertical: 12),
                        ),
                        items: IncidentType.values
                            .map((t) => DropdownMenuItem(
                                  value: t,
                                  child: Text(_incidentLabel(t)),
                                ))
                            .toList(),
                        onChanged: (v) => setState(() => _incidentType = v),
                        validator: (v) =>
                            _hasIncident && v == null ? 'Required' : null,
                      ),
                      const SizedBox(height: 10),
                      TextFormField(
                        controller: _incidentDescController,
                        maxLines: 3,
                        decoration: const InputDecoration(
                          labelText: 'Description *',
                          isDense: true,
                          filled: true,
                          fillColor: Color(0xFFF9FAFB),
                          border: OutlineInputBorder(),
                          contentPadding: EdgeInsets.symmetric(
                              horizontal: 14, vertical: 12),
                        ),
                        validator: (v) =>
                            _hasIncident && (v == null || v.trim().isEmpty)
                                ? 'Required'
                                : null,
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 16),

              // Additional notes
              _Card(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const _CardTitle(
                        'Additional Notes', Icons.notes_rounded),
                    const SizedBox(height: 10),
                    TextFormField(
                      controller: _additionalNotesController,
                      maxLines: 3,
                      decoration: const InputDecoration(
                        hintText: 'Any other observations…',
                        isDense: true,
                        filled: true,
                        fillColor: Color(0xFFF9FAFB),
                        border: OutlineInputBorder(),
                        contentPadding:
                            EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),

              // Audit trail
              if (tripAsync.valueOrNull != null) ...[
                _Card(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const _CardTitle('Audit Trail', Icons.history_rounded),
                      const SizedBox(height: 12),
                      _AuditTrail(trip: tripAsync.valueOrNull!),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
              ],

              // Billing / Ready to Invoice
              _Card(
                child: SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text(
                    'Ready to Invoice',
                    style: TextStyle(
                        fontSize: 14, fontWeight: FontWeight.w600),
                  ),
                  subtitle: const Text(
                    'Mark this trip as billable',
                    style: TextStyle(fontSize: 12, color: AppColors.brandGray),
                  ),
                  value: _isReadyToInvoice,
                  onChanged: (v) => setState(() => _isReadyToInvoice = v),
                  activeColor: AppColors.success,
                ),
              ),
            ],
          ),
        ),
      ),
      bottomNavigationBar: SafeArea(
        child: Container(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
          decoration: const BoxDecoration(
            color: Colors.white,
            border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
          ),
          child: SizedBox(
            width: double.infinity,
            child: FilledButton.icon(
              onPressed: _isSaving ? null : _submit,
              icon: _isSaving
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(
                          strokeWidth: 2, color: Colors.white),
                    )
                  : const Icon(Icons.check_circle_rounded),
              label: const Text('Submit Report'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                padding: const EdgeInsets.symmetric(vertical: 14),
                textStyle: const TextStyle(
                    fontSize: 16, fontWeight: FontWeight.w700),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _isSaving = true);
    try {
      final params = SubmitPostReportParams(
        odometerEnd: int.parse(_odometerEndController.text),
        fuelAddedLitres: _fuelLitresController.text.isNotEmpty
            ? double.tryParse(_fuelLitresController.text)
            : null,
        fuelCostDollars: _fuelCostController.text.isNotEmpty
            ? double.tryParse(_fuelCostController.text)
            : null,
        hasIncident: _hasIncident,
        incidentType: _incidentType,
        incidentDescription: _hasIncident &&
                _incidentDescController.text.trim().isNotEmpty
            ? _incidentDescController.text.trim()
            : null,
        additionalNotes: _additionalNotesController.text.trim().isNotEmpty
            ? _additionalNotesController.text.trim()
            : null,
        isReadyToInvoice: _isReadyToInvoice,
      );
      await ref
          .read(tripFormProvider)
          .submitPostReport(widget.tripId, params);

      ref.invalidate(tripDetailProvider(widget.tripId));
      ref.invalidate(tripsProvider);

      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text('Error: $e'),
              backgroundColor: AppColors.danger),
        );
      }
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }

  static String _incidentLabel(IncidentType t) => switch (t) {
        IncidentType.delay => 'Delay',
        IncidentType.passengerNoShow => 'Passenger No Show',
        IncidentType.vehicleIssue => 'Vehicle Issue',
        IncidentType.cargoDamage => 'Cargo Damage',
        IncidentType.accident => 'Accident',
      };
}

class _AuditTrail extends StatelessWidget {
  final dynamic trip;
  const _AuditTrail({required this.trip});

  @override
  Widget build(BuildContext context) {
    final fmt = DateFormat('MMM d, yyyy · h:mm a');
    return Column(
      children: [
        _AuditRow('Created', fmt.format((trip.createdAt as DateTime).toLocal())),
        if (trip.preInspection != null)
          _AuditRow('Inspected',
              fmt.format((trip.preInspection.submittedAt as DateTime).toLocal())),
      ],
    );
  }
}

class _AuditRow extends StatelessWidget {
  final String label;
  final String value;
  const _AuditRow(this.label, this.value);

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 6),
        child: Row(
          children: [
            SizedBox(
              width: 90,
              child: Text(
                label,
                style: const TextStyle(
                    fontSize: 12, color: AppColors.textSecondary),
              ),
            ),
            Text(
              value,
              style: const TextStyle(
                  fontSize: 12, color: AppColors.textPrimary),
            ),
          ],
        ),
      );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

class _Card extends StatelessWidget {
  final Widget child;
  const _Card({required this.child});

  @override
  Widget build(BuildContext context) => Container(
        width: double.infinity,
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: const Color(0xFFE5E7EB)),
        ),
        child: child,
      );
}

class _CardTitle extends StatelessWidget {
  final String title;
  final IconData icon;
  const _CardTitle(this.title, this.icon);

  @override
  Widget build(BuildContext context) => Row(
        children: [
          Icon(icon, size: 16, color: AppColors.primary),
          const SizedBox(width: 6),
          Text(
            title,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
        ],
      );
}

class _InfoRow extends StatelessWidget {
  final String label;
  final String value;
  const _InfoRow(this.label, this.value);

  @override
  Widget build(BuildContext context) => Row(
        children: [
          SizedBox(
            width: 80,
            child: Text(
              label,
              style: const TextStyle(
                  fontSize: 13, color: AppColors.textSecondary),
            ),
          ),
          Text(
            value,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
            ),
          ),
        ],
      );
}

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/delay_entry.dart';
import '../../domain/entities/trip_post_report.dart';

/// Shows a dialog to log a mid-trip delay.
/// Returns a [DelayEntry] on confirm, null on dismiss.
Future<DelayEntry?> showTripDelayDialog(BuildContext context) {
  return showDialog<DelayEntry>(
    context: context,
    builder: (_) => const _TripDelayDialog(),
  );
}

class _TripDelayDialog extends StatefulWidget {
  const _TripDelayDialog();

  @override
  State<_TripDelayDialog> createState() => _TripDelayDialogState();
}

class _TripDelayDialogState extends State<_TripDelayDialog> {
  final _formKey = GlobalKey<FormState>();
  final _descController = TextEditingController();
  final _minutesController = TextEditingController();
  IncidentType _type = IncidentType.delay;
  final _loggedAt = DateTime.now();

  @override
  void dispose() {
    _descController.dispose();
    _minutesController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Row(
        children: [
          Icon(Icons.warning_amber_rounded, color: AppColors.warning, size: 22),
          SizedBox(width: 8),
          Text('Report Delay', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
        ],
      ),
      content: SizedBox(
        width: 420,
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Timestamp
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                decoration: BoxDecoration(
                  color: const Color(0xFFF9FAFB),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: const Color(0xFFE5E7EB)),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.access_time_rounded,
                        size: 16, color: AppColors.textSecondary),
                    const SizedBox(width: 6),
                    Text(
                      DateFormat('MMM d · h:mm a').format(_loggedAt),
                      style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w500,
                          color: AppColors.textSecondary),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 14),
              // Delay type
              DropdownButtonFormField<IncidentType>(
                value: _type,
                decoration: const InputDecoration(
                  labelText: 'Delay Type *',
                  isDense: true,
                  filled: true,
                  fillColor: Color(0xFFF9FAFB),
                  border: OutlineInputBorder(),
                  contentPadding: EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                ),
                items: IncidentType.values
                    .map((t) => DropdownMenuItem(value: t, child: Text(_label(t))))
                    .toList(),
                onChanged: (v) => setState(() => _type = v ?? _type),
              ),
              const SizedBox(height: 12),
              // Description
              TextFormField(
                controller: _descController,
                maxLines: 3,
                decoration: const InputDecoration(
                  labelText: 'Description *',
                  hintText: 'Briefly describe the delay…',
                  isDense: true,
                  filled: true,
                  fillColor: Color(0xFFF9FAFB),
                  border: OutlineInputBorder(),
                  contentPadding: EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                ),
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? 'Required' : null,
              ),
              const SizedBox(height: 12),
              // Estimated minutes
              TextFormField(
                controller: _minutesController,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                decoration: const InputDecoration(
                  labelText: 'Estimated Delay (minutes)',
                  suffixText: 'min',
                  isDense: true,
                  filled: true,
                  fillColor: Color(0xFFF9FAFB),
                  border: OutlineInputBorder(),
                  contentPadding: EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                ),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: _confirm,
          style: FilledButton.styleFrom(backgroundColor: AppColors.warning),
          child: const Text('Log Delay', style: TextStyle(color: Colors.white)),
        ),
      ],
    );
  }

  void _confirm() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop(
      DelayEntry(
        loggedAt: _loggedAt,
        type: _type,
        description: _descController.text.trim(),
        estimatedMinutes: int.tryParse(_minutesController.text) ?? 0,
      ),
    );
  }

  static String _label(IncidentType t) => switch (t) {
        IncidentType.delay => 'General Delay',
        IncidentType.passengerNoShow => 'Passenger No Show',
        IncidentType.vehicleIssue => 'Vehicle Issue',
        IncidentType.cargoDamage => 'Cargo / Cargo Damage',
        IncidentType.accident => 'Accident',
      };
}

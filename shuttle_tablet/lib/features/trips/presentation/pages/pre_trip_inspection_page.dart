import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../providers/trip_form_provider.dart';

// Predefined inspection checklist items
const _exteriorItems = [
  'Lights (headlights, brake lights, indicators)',
  'Tires (pressure, tread, condition)',
  'Mirrors (clean, properly adjusted)',
  'Windshield (no cracks or obstructions)',
  'Body Damage (dents, scratches, damage)',
  'Horn (functional)',
  'Windshield Wipers',
  'Fuel Level',
];

const _interiorItems = [
  'Seat Belts (all rows)',
  'Emergency Exit (accessible, marked)',
  'First Aid Kit (stocked, accessible)',
  'Fire Extinguisher (charged, accessible)',
  'Interior Cleanliness',
];

class PreTripInspectionPage extends ConsumerStatefulWidget {
  final String tripId;
  const PreTripInspectionPage({super.key, required this.tripId});

  @override
  ConsumerState<PreTripInspectionPage> createState() =>
      _PreTripInspectionPageState();
}

class _PreTripInspectionPageState
    extends ConsumerState<PreTripInspectionPage> {
  final _odometerController = TextEditingController();
  final _formKey = GlobalKey<FormState>();

  // Item states: null = not reviewed, true = passed, false = failed
  final Map<String, bool?> _results = {};
  final Map<String, TextEditingController> _noteControllers = {};
  bool _isSaving = false;

  @override
  void initState() {
    super.initState();
    for (final item in [..._exteriorItems, ..._interiorItems]) {
      _results[item] = null;
      _noteControllers[item] = TextEditingController();
    }
  }

  @override
  void dispose() {
    _odometerController.dispose();
    for (final c in _noteControllers.values) {
      c.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final failCount =
        _results.values.where((v) => v == false).length;
    final reviewedCount =
        _results.values.where((v) => v != null).length;
    final totalCount = _results.length;
    final allReviewed = reviewedCount == totalCount;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          'Pre-Trip Inspection',
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
              // Odometer
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
                      'Odometer Reading',
                      style: TextStyle(
                          fontSize: 14, fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 10),
                    TextFormField(
                      controller: _odometerController,
                      keyboardType: TextInputType.number,
                      inputFormatters: [
                        FilteringTextInputFormatter.digitsOnly
                      ],
                      decoration: const InputDecoration(
                        hintText: 'Enter starting odometer (km)',
                        suffixText: 'km',
                        isDense: true,
                        filled: true,
                        fillColor: Color(0xFFF9FAFB),
                        border: OutlineInputBorder(),
                        contentPadding: EdgeInsets.symmetric(
                            horizontal: 14, vertical: 12),
                      ),
                      validator: (v) {
                        if (v == null || v.isEmpty) return 'Required';
                        if (int.tryParse(v) == null) return 'Enter a number';
                        return null;
                      },
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 20),

              // Exterior
              _SectionHeader('Exterior Inspection'),
              const SizedBox(height: 10),
              ..._exteriorItems.map((item) => _ItemRow(
                    item: item,
                    result: _results[item],
                    noteController: _noteControllers[item]!,
                    onPass: () => setState(() => _results[item] = true),
                    onFail: () => setState(() => _results[item] = false),
                  )),

              const SizedBox(height: 20),

              // Interior (collapsible)
              ExpansionTile(
                initiallyExpanded: false,
                title: const Text(
                  'Interior Inspection',
                  style: TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                backgroundColor: Colors.white,
                collapsedBackgroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                  side: const BorderSide(color: Color(0xFFE5E7EB)),
                ),
                collapsedShape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                  side: const BorderSide(color: Color(0xFFE5E7EB)),
                ),
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                    child: Column(
                      children: _interiorItems.map((item) => _ItemRow(
                            item: item,
                            result: _results[item],
                            noteController: _noteControllers[item]!,
                            onPass: () =>
                                setState(() => _results[item] = true),
                            onFail: () =>
                                setState(() => _results[item] = false),
                          )).toList(),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
      // Sticky footer
      bottomNavigationBar: SafeArea(
        child: Container(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
          decoration: const BoxDecoration(
            color: Colors.white,
            border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              // Progress + issues summary
              Row(
                children: [
                  Expanded(
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(4),
                      child: LinearProgressIndicator(
                        value: totalCount > 0 ? reviewedCount / totalCount : 0,
                        minHeight: 6,
                        backgroundColor: const Color(0xFFE5E7EB),
                        color: allReviewed
                            ? (failCount > 0 ? AppColors.warning : AppColors.success)
                            : AppColors.primary,
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Text(
                    '$reviewedCount/$totalCount reviewed',
                    style: const TextStyle(
                        fontSize: 12, color: AppColors.textSecondary),
                  ),
                  if (failCount > 0) ...[
                    const SizedBox(width: 8),
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 2),
                      decoration: BoxDecoration(
                        color: const Color(0xFFFEF3C7),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Text(
                        '$failCount issue${failCount == 1 ? '' : 's'}',
                        style: const TextStyle(
                          fontSize: 11,
                          color: Color(0xFF92400E),
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: (!allReviewed || _isSaving) ? null : _submit,
                  icon: _isSaving
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(
                              strokeWidth: 2, color: Colors.white),
                        )
                      : const Icon(Icons.check_circle_rounded),
                  label: Text(allReviewed
                      ? 'Submit Inspection'
                      : 'Review all items to submit'),
                  style: FilledButton.styleFrom(
                    backgroundColor: allReviewed
                        ? AppColors.primary
                        : AppColors.brandGray,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _isSaving = true);
    try {
      final items = _results.entries
          .map((e) => InspectionItemParams(
                itemName: e.key,
                passed: e.value!,
                notes: _noteControllers[e.key]!.text.trim().isEmpty
                    ? null
                    : _noteControllers[e.key]!.text.trim(),
              ))
          .toList();

      await ref.read(tripFormProvider.notifier).submitPreInspection(
            widget.tripId,
            SubmitPreInspectionParams(
              odometerStart: int.parse(_odometerController.text),
              items: items,
            ),
          );
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
}

// ── Widgets ───────────────────────────────────────────────────────────────────

class _SectionHeader extends StatelessWidget {
  final String title;
  const _SectionHeader(this.title);

  @override
  Widget build(BuildContext context) => Text(
        title,
        style: const TextStyle(
          fontSize: 15,
          fontWeight: FontWeight.w700,
          color: AppColors.textPrimary,
        ),
      );
}

class _ItemRow extends StatelessWidget {
  final String item;
  final bool? result; // null = not reviewed, true = pass, false = fail
  final TextEditingController noteController;
  final VoidCallback onPass;
  final VoidCallback onFail;

  const _ItemRow({
    required this.item,
    required this.result,
    required this.noteController,
    required this.onPass,
    required this.onFail,
  });

  @override
  Widget build(BuildContext context) {
    final isFail = result == false;
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: result == null
            ? Colors.white
            : result!
                ? const Color(0xFFF0FDF4)
                : const Color(0xFFFEF2F2),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(
          color: result == null
              ? const Color(0xFFE5E7EB)
              : result!
                  ? const Color(0xBBBBF7D0)
                  : const Color(0xBBFECACA),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  item,
                  style: const TextStyle(
                      fontSize: 13, fontWeight: FontWeight.w500),
                ),
              ),
              const SizedBox(width: 8),
              _PassFailButton(
                label: 'Pass',
                selected: result == true,
                color: AppColors.success,
                onTap: onPass,
              ),
              const SizedBox(width: 6),
              _PassFailButton(
                label: 'Fail',
                selected: result == false,
                color: AppColors.danger,
                onTap: onFail,
              ),
            ],
          ),
          if (isFail) ...[
            const SizedBox(height: 8),
            TextField(
              controller: noteController,
              maxLines: 2,
              decoration: const InputDecoration(
                hintText: 'Describe the issue…',
                isDense: true,
                filled: true,
                fillColor: Colors.white,
                border: OutlineInputBorder(),
                contentPadding:
                    EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _PassFailButton extends StatelessWidget {
  final String label;
  final bool selected;
  final Color color;
  final VoidCallback onTap;

  const _PassFailButton({
    required this.label,
    required this.selected,
    required this.color,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          padding:
              const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
          decoration: BoxDecoration(
            color: selected ? color : Colors.white,
            borderRadius: BorderRadius.circular(6),
            border: Border.all(color: selected ? color : const Color(0xFFD1D5DB)),
          ),
          child: Text(
            label,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: selected ? Colors.white : AppColors.textSecondary,
            ),
          ),
        ),
      );
}

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip_inspection_item.dart';
import '../../domain/entities/trip_pre_inspection.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../providers/trip_form_provider.dart';

// ── Inspection checklist definitions ─────────────────────────────────────────

class _CheckItem {
  final String label;
  final InspectionCategory category;
  const _CheckItem(this.label, this.category);
}

const _inspectionItems = [
  // Exterior & Mechanical
  _CheckItem('Tires — condition & pressure', InspectionCategory.exteriorMechanical),
  _CheckItem('Lights — headlights, brake, turn, hazard', InspectionCategory.exteriorMechanical),
  _CheckItem('Windshield & mirrors — clean, no cracks', InspectionCategory.exteriorMechanical),
  _CheckItem('Wipers & washer fluid', InspectionCategory.exteriorMechanical),
  _CheckItem('Brakes — tested, no warning lights', InspectionCategory.exteriorMechanical),
  _CheckItem('Horn — functioning', InspectionCategory.exteriorMechanical),
  _CheckItem('Exhaust — no leaks or damage', InspectionCategory.exteriorMechanical),
  _CheckItem('Fluid levels — oil, coolant, brake', InspectionCategory.exteriorMechanical),
  _CheckItem('Battery — secure, no corrosion', InspectionCategory.exteriorMechanical),
  _CheckItem('Block heater cord — intact (winter)', InspectionCategory.exteriorMechanical),
  // Safety Equipment
  _CheckItem('First aid kit — stocked & accessible', InspectionCategory.safetyEquipment),
  _CheckItem('Fire extinguisher — charged & mounted', InspectionCategory.safetyEquipment),
  _CheckItem('Emergency triangles / flares', InspectionCategory.safetyEquipment),
  _CheckItem('Spill kit (mine requirement)', InspectionCategory.safetyEquipment),
  _CheckItem('Safety beacon / whip flag', InspectionCategory.safetyEquipment),
  _CheckItem('Backup alarm — functioning', InspectionCategory.safetyEquipment),
  _CheckItem('Seatbelts — all functioning', InspectionCategory.safetyEquipment),
  _CheckItem('Cargo restraints / tie-downs', InspectionCategory.safetyEquipment),
  _CheckItem('Reflective safety vest', InspectionCategory.safetyEquipment),
  _CheckItem('Jumper cables / booster pack', InspectionCategory.safetyEquipment),
  // Interior & Comfort
  _CheckItem('Interior clean & clear of debris', InspectionCategory.interiorComfort),
  _CheckItem('Heat / AC — functioning', InspectionCategory.interiorComfort),
  _CheckItem('Doors — open/close/lock properly', InspectionCategory.interiorComfort),
  _CheckItem('Dashboard — no warning lights', InspectionCategory.interiorComfort),
  // Communications & Navigation
  _CheckItem('Cell phone — charged', InspectionCategory.communicationsNavigation),
  _CheckItem('Starlink / satellite comm — connected', InspectionCategory.communicationsNavigation),
  _CheckItem('GPS / navigation — functioning', InspectionCategory.communicationsNavigation),
  _CheckItem('Emergency contacts list — in vehicle', InspectionCategory.communicationsNavigation),
];

const _categoryLabels = {
  InspectionCategory.exteriorMechanical: 'Exterior & Mechanical',
  InspectionCategory.safetyEquipment: 'Safety Equipment',
  InspectionCategory.interiorComfort: 'Interior & Comfort',
  InspectionCategory.communicationsNavigation: 'Communications & Navigation',
};

// ── Page ──────────────────────────────────────────────────────────────────────

class PreTripInspectionPage extends ConsumerStatefulWidget {
  final String tripId;
  const PreTripInspectionPage({super.key, required this.tripId});

  @override
  ConsumerState<PreTripInspectionPage> createState() =>
      _PreTripInspectionPageState();
}

class _PreTripInspectionPageState extends ConsumerState<PreTripInspectionPage> {
  final _odometerController = TextEditingController();
  final _temperatureController = TextEditingController();
  final _advisoriesController = TextEditingController();
  final _formKey = GlobalKey<FormState>();

  final Map<String, bool?> _results = {};
  final Map<String, TextEditingController> _noteControllers = {};

  FuelLevel _fuelLevel = FuelLevel.full;
  String? _weatherType;
  String? _roadConditions;
  String? _visibility;
  DateTime? _weatherPulledAt;

  bool _isSaving = false;
  bool _isFetchingWeather = false;

  @override
  void initState() {
    super.initState();
    for (final item in _inspectionItems) {
      _results[item.label] = null;
      _noteControllers[item.label] = TextEditingController();
    }
  }

  @override
  void dispose() {
    _odometerController.dispose();
    _temperatureController.dispose();
    _advisoriesController.dispose();
    for (final c in _noteControllers.values) {
      c.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final failCount = _results.values.where((v) => v == false).length;
    final reviewedCount = _results.values.where((v) => v != null).length;
    final totalCount = _results.length;
    final allReviewed = reviewedCount == totalCount;

    final categories = InspectionCategory.values;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          'Pre-Trip Inspection',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: AppColors.textPrimary),
        ),
      ),
      body: Form(
        key: _formKey,
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(20, 20, 20, 120),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _OdometerCard(controller: _odometerController),
              const SizedBox(height: 16),
              _FuelLevelCard(
                selected: _fuelLevel,
                onChanged: (v) => setState(() => _fuelLevel = v),
              ),
              const SizedBox(height: 16),

              // Inspection categories
              for (final cat in categories) ...[
                _CategorySection(
                  title: _categoryLabels[cat]!,
                  items: _inspectionItems.where((i) => i.category == cat).toList(),
                  results: _results,
                  noteControllers: _noteControllers,
                  onToggle: (label, passed) =>
                      setState(() => _results[label] = passed),
                ),
                const SizedBox(height: 16),
              ],

              // Weather section
              _WeatherSection(
                weatherType: _weatherType,
                temperature: _temperatureController,
                roadConditions: _roadConditions,
                visibility: _visibility,
                advisories: _advisoriesController,
                weatherPulledAt: _weatherPulledAt,
                isFetching: _isFetchingWeather,
                onWeatherTypeChanged: (v) => setState(() => _weatherType = v),
                onRoadConditionsChanged: (v) =>
                    setState(() => _roadConditions = v),
                onVisibilityChanged: (v) => setState(() => _visibility = v),
                onFetchWeather: _fetchWeather,
              ),
            ],
          ),
        ),
      ),
      bottomNavigationBar: _SubmitBar(
        reviewedCount: reviewedCount,
        totalCount: totalCount,
        failCount: failCount,
        allReviewed: allReviewed,
        isSaving: _isSaving,
        onSubmit: _submit,
      ),
    );
  }

  Future<void> _fetchWeather() async {
    setState(() => _isFetchingWeather = true);
    try {
      final permission = await Geolocator.checkPermission();
      LocationPermission perm = permission;
      if (perm == LocationPermission.denied) {
        perm = await Geolocator.requestPermission();
      }
      if (perm == LocationPermission.deniedForever ||
          perm == LocationPermission.denied) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content: Text('Location permission denied — enter weather manually'),
          ));
        }
        return;
      }

      final pos = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.low,
          timeLimit: Duration(seconds: 10),
        ),
      );

      final lat = pos.latitude;
      final lon = pos.longitude;
      final url =
          'https://api.weather.gc.ca/collections/swob-realtime/items?f=json&limit=1&sortby=-dataDate'
          '&bbox=${lon - 0.5},${lat - 0.5},${lon + 0.5},${lat + 0.5}';

      final dio = Dio();
      final response = await dio.get(url);
      final features = (response.data['features'] as List?)??[];

      if (features.isNotEmpty) {
        final props =
            (features.first as Map)['properties'] as Map<String, dynamic>?;
        if (props != null) {
          final temp = props['air_temp'];
          final cond = props['weather_condition_abb'] as String?;
          final vis = props['visibility_obb'];

          setState(() {
            if (temp != null) {
              _temperatureController.text = '$temp°C';
            }
            if (cond != null) {
              _weatherType = _mapWeatherCondition(cond);
            }
            if (vis != null) {
              final visKm = double.tryParse(vis.toString()) ?? 999.0;
              _visibility = visKm >= 8
                  ? 'Good'
                  : visKm >= 3
                      ? 'Reduced'
                      : 'Poor';
            }
            _weatherPulledAt = DateTime.now().toUtc();
          });
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Weather conditions updated from MSC GeoMet'),
                backgroundColor: AppColors.success,
              ),
            );
          }
          return;
        }
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('No nearby weather station found — enter manually'),
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Weather fetch failed: $e — enter manually')),
        );
      }
    } finally {
      if (mounted) setState(() => _isFetchingWeather = false);
    }
  }

  String? _mapWeatherCondition(String abbr) {
    final lower = abbr.toLowerCase();
    if (lower.contains('clear') || lower.contains('sunny')) return 'Clear';
    if (lower.contains('cloud') || lower.contains('overcast')) return 'Cloudy';
    if (lower.contains('rain') || lower.contains('drizzle')) return 'Rain';
    if (lower.contains('snow') || lower.contains('flurr')) return 'Snow';
    if (lower.contains('fog') || lower.contains('mist')) return 'Fog';
    if (lower.contains('freeze') || lower.contains('ice')) return 'ExtremeCold';
    return null;
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _isSaving = true);
    try {
      final items = _inspectionItems
          .map((item) => InspectionItemParams(
                itemName: item.label,
                category: item.category,
                passed: _results[item.label]!,
                notes: _noteControllers[item.label]!.text.trim().isEmpty
                    ? null
                    : _noteControllers[item.label]!.text.trim(),
              ))
          .toList();

      await ref.read(tripFormProvider).submitPreInspection(
            widget.tripId,
            SubmitPreInspectionParams(
              odometerStart: int.parse(_odometerController.text),
              fuelLevel: _fuelLevel,
              weatherType: _weatherType,
              temperature: _temperatureController.text.trim().isEmpty
                  ? null
                  : _temperatureController.text.trim(),
              roadConditions: _roadConditions,
              visibility: _visibility,
              roadAdvisories: _advisoriesController.text.trim().isEmpty
                  ? null
                  : _advisoriesController.text.trim(),
              weatherPulledAt: _weatherPulledAt,
              items: items,
            ),
          );
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

// ── Sub-widgets ───────────────────────────────────────────────────────────────

class _OdometerCard extends StatelessWidget {
  final TextEditingController controller;
  const _OdometerCard({required this.controller});

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(16),
        decoration: _cardDecoration(),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Odometer Reading',
                style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
            const SizedBox(height: 10),
            TextFormField(
              controller: controller,
              keyboardType: TextInputType.number,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: const InputDecoration(
                hintText: 'Enter starting odometer (km)',
                suffixText: 'km',
                isDense: true,
                filled: true,
                fillColor: Color(0xFFF9FAFB),
                border: OutlineInputBorder(),
                contentPadding:
                    EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              ),
              validator: (v) {
                if (v == null || v.isEmpty) return 'Required';
                if (int.tryParse(v) == null) return 'Enter a number';
                return null;
              },
            ),
          ],
        ),
      );
}

class _FuelLevelCard extends StatelessWidget {
  final FuelLevel selected;
  final ValueChanged<FuelLevel> onChanged;
  const _FuelLevelCard({required this.selected, required this.onChanged});

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(16),
        decoration: _cardDecoration(),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Fuel Level',
                style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
            const SizedBox(height: 12),
            Row(
              children: FuelLevel.values.map((level) {
                final label = switch (level) {
                  FuelLevel.full => 'Full',
                  FuelLevel.threeQuarters => '¾',
                  FuelLevel.half => '½',
                  FuelLevel.quarter => '¼',
                };
                final isSelected = selected == level;
                return Expanded(
                  child: GestureDetector(
                    onTap: () => onChanged(level),
                    child: AnimatedContainer(
                      duration: const Duration(milliseconds: 150),
                      margin: const EdgeInsets.symmetric(horizontal: 3),
                      padding: const EdgeInsets.symmetric(vertical: 10),
                      decoration: BoxDecoration(
                        color: isSelected ? AppColors.primary : Colors.white,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(
                            color: isSelected
                                ? AppColors.primary
                                : const Color(0xFFD1D5DB)),
                      ),
                      alignment: Alignment.center,
                      child: Text(
                        label,
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color:
                              isSelected ? Colors.white : AppColors.textPrimary,
                        ),
                      ),
                    ),
                  ),
                );
              }).toList(),
            ),
          ],
        ),
      );
}

class _CategorySection extends StatelessWidget {
  final String title;
  final List<_CheckItem> items;
  final Map<String, bool?> results;
  final Map<String, TextEditingController> noteControllers;
  final void Function(String label, bool? passed) onToggle;

  const _CategorySection({
    required this.title,
    required this.items,
    required this.results,
    required this.noteControllers,
    required this.onToggle,
  });

  @override
  Widget build(BuildContext context) {
    return ExpansionTile(
      initiallyExpanded: true,
      title: Text(
        title,
        style: const TextStyle(
            fontSize: 15, fontWeight: FontWeight.w700, color: AppColors.textPrimary),
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
            children: items
                .map((item) => _ItemRow(
                      item: item.label,
                      result: results[item.label],
                      noteController: noteControllers[item.label]!,
                      onPass: () => onToggle(item.label, true),
                      onFail: () => onToggle(item.label, false),
                    ))
                .toList(),
          ),
        ),
      ],
    );
  }
}

class _WeatherSection extends StatelessWidget {
  final String? weatherType;
  final TextEditingController temperature;
  final String? roadConditions;
  final String? visibility;
  final TextEditingController advisories;
  final DateTime? weatherPulledAt;
  final bool isFetching;
  final ValueChanged<String?> onWeatherTypeChanged;
  final ValueChanged<String?> onRoadConditionsChanged;
  final ValueChanged<String?> onVisibilityChanged;
  final VoidCallback onFetchWeather;

  const _WeatherSection({
    required this.weatherType,
    required this.temperature,
    required this.roadConditions,
    required this.visibility,
    required this.advisories,
    required this.weatherPulledAt,
    required this.isFetching,
    required this.onWeatherTypeChanged,
    required this.onRoadConditionsChanged,
    required this.onVisibilityChanged,
    required this.onFetchWeather,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: _cardDecoration(),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Expanded(
                child: Text('Weather & Road Conditions',
                    style:
                        TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
              ),
              FilledButton.icon(
                onPressed: isFetching ? null : onFetchWeather,
                icon: isFetching
                    ? const SizedBox(
                        width: 14,
                        height: 14,
                        child: CircularProgressIndicator(
                            strokeWidth: 2, color: Colors.white))
                    : const Icon(Icons.cloud_download_outlined, size: 16),
                label:
                    Text(isFetching ? 'Fetching…' : 'Fetch Current'),
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.primary,
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                  textStyle: const TextStyle(fontSize: 12),
                ),
              ),
            ],
          ),
          if (weatherPulledAt != null)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                'Pulled at ${_formatTime(weatherPulledAt!)} — override values as needed',
                style: const TextStyle(
                    fontSize: 11, color: AppColors.textSecondary),
              ),
            ),
          const SizedBox(height: 14),
          _ChipRow(
            label: 'Weather',
            options: const [
              'Clear', 'Cloudy', 'Rain', 'Snow', 'Fog', 'Extreme Cold'
            ],
            selected: weatherType,
            onSelected: onWeatherTypeChanged,
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: temperature,
            decoration: const InputDecoration(
              labelText: 'Temperature',
              hintText: 'e.g. -12°C',
              isDense: true,
              filled: true,
              fillColor: Color(0xFFF9FAFB),
              border: OutlineInputBorder(),
              contentPadding:
                  EdgeInsets.symmetric(horizontal: 14, vertical: 12),
            ),
          ),
          const SizedBox(height: 12),
          _ChipRow(
            label: 'Road Conditions',
            options: const ['Dry', 'Wet', 'Icy', 'Snow-covered', 'Muddy'],
            selected: roadConditions,
            onSelected: onRoadConditionsChanged,
          ),
          const SizedBox(height: 12),
          _ChipRow(
            label: 'Visibility',
            options: const ['Good', 'Reduced', 'Poor'],
            selected: visibility,
            onSelected: onVisibilityChanged,
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: advisories,
            maxLines: 2,
            decoration: const InputDecoration(
              labelText: 'Road Advisories',
              hintText: 'Any active advisories or closures…',
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
    );
  }

  String _formatTime(DateTime dt) {
    final local = dt.toLocal();
    return '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
  }
}

class _ChipRow extends StatelessWidget {
  final String label;
  final List<String> options;
  final String? selected;
  final ValueChanged<String?> onSelected;
  const _ChipRow(
      {required this.label,
      required this.options,
      required this.selected,
      required this.onSelected});

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label,
              style: const TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textSecondary)),
          const SizedBox(height: 6),
          Wrap(
            spacing: 6,
            runSpacing: 4,
            children: options
                .map((opt) => ChoiceChip(
                      label: Text(opt,
                          style: TextStyle(
                              fontSize: 12,
                              color: selected == opt
                                  ? Colors.white
                                  : AppColors.textPrimary)),
                      selected: selected == opt,
                      selectedColor: AppColors.primary,
                      backgroundColor: Colors.white,
                      side: BorderSide(
                          color: selected == opt
                              ? AppColors.primary
                              : const Color(0xFFD1D5DB)),
                      onSelected: (v) => onSelected(v ? opt : null),
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 2),
                    ))
                .toList(),
          ),
        ],
      );
}

class _SubmitBar extends StatelessWidget {
  final int reviewedCount;
  final int totalCount;
  final int failCount;
  final bool allReviewed;
  final bool isSaving;
  final VoidCallback onSubmit;

  const _SubmitBar({
    required this.reviewedCount,
    required this.totalCount,
    required this.failCount,
    required this.allReviewed,
    required this.isSaving,
    required this.onSubmit,
  });

  @override
  Widget build(BuildContext context) => SafeArea(
        child: Container(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
          decoration: const BoxDecoration(
            color: Colors.white,
            border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                children: [
                  Expanded(
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(4),
                      child: LinearProgressIndicator(
                        value:
                            totalCount > 0 ? reviewedCount / totalCount : 0,
                        minHeight: 6,
                        backgroundColor: const Color(0xFFE5E7EB),
                        color: allReviewed
                            ? (failCount > 0
                                ? AppColors.warning
                                : AppColors.success)
                            : AppColors.primary,
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Text('$reviewedCount/$totalCount reviewed',
                      style: const TextStyle(
                          fontSize: 12, color: AppColors.textSecondary)),
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
                  onPressed: (!allReviewed || isSaving) ? null : onSubmit,
                  icon: isSaving
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(
                              strokeWidth: 2, color: Colors.white))
                      : const Icon(Icons.check_circle_rounded),
                  label: Text(allReviewed
                      ? 'Submit Inspection'
                      : 'Review all items to submit'),
                  style: FilledButton.styleFrom(
                    backgroundColor:
                        allReviewed ? AppColors.primary : AppColors.brandGray,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                  ),
                ),
              ),
            ],
          ),
        ),
      );
}

class _ItemRow extends StatelessWidget {
  final String item;
  final bool? result;
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
                  child: Text(item,
                      style: const TextStyle(
                          fontSize: 13, fontWeight: FontWeight.w500))),
              const SizedBox(width: 8),
              _PassFailButton(
                  label: 'Pass',
                  selected: result == true,
                  color: AppColors.success,
                  onTap: onPass),
              const SizedBox(width: 6),
              _PassFailButton(
                  label: 'Fail',
                  selected: result == false,
                  color: AppColors.danger,
                  onTap: onFail),
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

  const _PassFailButton(
      {required this.label,
      required this.selected,
      required this.color,
      required this.onTap});

  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
          decoration: BoxDecoration(
            color: selected ? color : Colors.white,
            borderRadius: BorderRadius.circular(6),
            border: Border.all(
                color: selected ? color : const Color(0xFFD1D5DB)),
          ),
          child: Text(label,
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
                color: selected ? Colors.white : AppColors.textSecondary,
              )),
        ),
      );
}

BoxDecoration _cardDecoration() => BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      border: Border.all(color: const Color(0xFFE5E7EB)),
    );

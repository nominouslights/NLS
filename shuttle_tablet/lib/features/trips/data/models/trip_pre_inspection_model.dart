import '../../domain/entities/trip_pre_inspection.dart';
import 'trip_inspection_item_model.dart';

class TripPreInspectionModel extends TripPreInspection {
  const TripPreInspectionModel({
    required super.id,
    required super.tripId,
    required super.odometerStart,
    super.fuelLevel = FuelLevel.full,
    super.weatherType,
    super.temperature,
    super.roadConditions,
    super.visibility,
    super.roadAdvisories,
    super.weatherPulledAt,
    required super.submittedAt,
    super.items = const [],
  });

  factory TripPreInspectionModel.fromJson(Map<String, dynamic> json) {
    final itemsJson = json['items'] as List<dynamic>? ?? [];
    return TripPreInspectionModel(
      id: json['id'] as String,
      tripId: json['tripId'] as String? ?? '',
      odometerStart: json['odometerStart'] as int,
      fuelLevel: _parseFuelLevel(json['fuelLevel'] as String?),
      weatherType: json['weatherType'] as String?,
      temperature: json['temperature'] as String?,
      roadConditions: json['roadConditions'] as String?,
      visibility: json['visibility'] as String?,
      roadAdvisories: json['roadAdvisories'] as String?,
      weatherPulledAt: json['weatherPulledAt'] != null
          ? DateTime.tryParse(json['weatherPulledAt'] as String)
          : null,
      submittedAt: DateTime.tryParse(json['submittedAt'] as String? ?? '') ??
          DateTime.now(),
      items: itemsJson
          .map((e) =>
              TripInspectionItemModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }

  static FuelLevel _parseFuelLevel(String? value) {
    switch (value) {
      case 'ThreeQuarters':
        return FuelLevel.threeQuarters;
      case 'Half':
        return FuelLevel.half;
      case 'Quarter':
        return FuelLevel.quarter;
      default:
        return FuelLevel.full;
    }
  }
}

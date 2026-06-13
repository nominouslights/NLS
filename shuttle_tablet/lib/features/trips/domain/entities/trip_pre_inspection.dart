import 'package:equatable/equatable.dart';
import 'trip_inspection_item.dart';

enum FuelLevel { full, threeQuarters, half, quarter }

class TripPreInspection extends Equatable {
  final String id;
  final String tripId;
  final int odometerStart;
  final FuelLevel fuelLevel;
  final String? weatherType;
  final String? temperature;
  final String? roadConditions;
  final String? visibility;
  final String? roadAdvisories;
  final DateTime? weatherPulledAt;
  final DateTime submittedAt;
  final List<TripInspectionItem> items;

  const TripPreInspection({
    required this.id,
    required this.tripId,
    required this.odometerStart,
    this.fuelLevel = FuelLevel.full,
    this.weatherType,
    this.temperature,
    this.roadConditions,
    this.visibility,
    this.roadAdvisories,
    this.weatherPulledAt,
    required this.submittedAt,
    this.items = const [],
  });

  @override
  List<Object?> get props => [
        id,
        tripId,
        odometerStart,
        fuelLevel,
        weatherType,
        temperature,
        roadConditions,
        visibility,
        roadAdvisories,
        weatherPulledAt,
        submittedAt,
        items,
      ];
}

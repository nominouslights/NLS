import '../../domain/entities/vehicle_odometer_entry.dart';

class VehicleOdometerEntryModel extends VehicleOdometerEntry {
  const VehicleOdometerEntryModel({
    required super.date,
    required super.odometerKm,
    required super.source,
    required super.referenceId,
    super.notes,
  });

  factory VehicleOdometerEntryModel.fromJson(Map<String, dynamic> json) {
    return VehicleOdometerEntryModel(
      date: DateTime.parse(json['date'] as String),
      odometerKm: json['odometerKm'] as int,
      source: json['source'] as String,
      referenceId: json['referenceId'] as String,
      notes: json['notes'] as String?,
    );
  }
}

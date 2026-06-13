import '../../domain/entities/vehicle_fuel_entry.dart';

class VehicleFuelEntryModel extends VehicleFuelEntry {
  const VehicleFuelEntryModel({
    required super.id,
    required super.vehicleId,
    required super.fuelledAt,
    required super.fuelLitres,
    required super.totalCostDollars,
    super.odometerAtFuelling,
    super.hasReceipt = false,
    super.notes,
    required super.createdAt,
  });

  factory VehicleFuelEntryModel.fromJson(
      Map<String, dynamic> json, String vehicleId) {
    return VehicleFuelEntryModel(
      id: json['id'] as String,
      vehicleId: vehicleId,
      fuelledAt: DateTime.parse(json['fuelledAt'] as String),
      fuelLitres: (json['fuelLitres'] as num).toDouble(),
      totalCostDollars: (json['totalCostDollars'] as num).toDouble(),
      odometerAtFuelling: json['odometerAtFuelling'] as int?,
      hasReceipt: json['receiptPhotoUrl'] != null,
      notes: json['notes'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}

import 'package:equatable/equatable.dart';

class VehicleFuelEntry extends Equatable {
  final String id;
  final String vehicleId;
  final DateTime fuelledAt;
  final double fuelLitres;
  final double totalCostDollars;
  final int? odometerAtFuelling;
  final bool hasReceipt;
  final String? notes;
  final DateTime createdAt;

  const VehicleFuelEntry({
    required this.id,
    required this.vehicleId,
    required this.fuelledAt,
    required this.fuelLitres,
    required this.totalCostDollars,
    this.odometerAtFuelling,
    this.hasReceipt = false,
    this.notes,
    required this.createdAt,
  });

  @override
  List<Object?> get props => [
        id,
        vehicleId,
        fuelledAt,
        fuelLitres,
        totalCostDollars,
        odometerAtFuelling,
        hasReceipt,
        notes,
        createdAt,
      ];
}

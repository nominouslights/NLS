import 'package:equatable/equatable.dart';

class ContractRateLine extends Equatable {
  final String id;
  final String contractId;
  final String billingCode;
  final String description;
  final String vehicleType;
  final int? maxDistanceKm;
  final bool cargoIncluded;
  final double dayRate;

  const ContractRateLine({
    required this.id,
    required this.contractId,
    required this.billingCode,
    required this.description,
    required this.vehicleType,
    this.maxDistanceKm,
    required this.cargoIncluded,
    required this.dayRate,
  });

  @override
  List<Object?> get props => [id, contractId, billingCode, description, vehicleType, maxDistanceKm, cargoIncluded, dayRate];
}

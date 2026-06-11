import '../../domain/entities/contract_rate_line.dart';

class ContractRateLineModel extends ContractRateLine {
  const ContractRateLineModel({
    required super.id,
    required super.contractId,
    required super.billingCode,
    required super.description,
    required super.vehicleType,
    super.maxDistanceKm,
    required super.cargoIncluded,
    required super.dayRate,
  });

  factory ContractRateLineModel.fromJson(
    Map<String, dynamic> json, {
    String? contractId,
  }) {
    return ContractRateLineModel(
      id: json['id'] as String,
      contractId: contractId ?? json['contractId'] as String? ?? '',
      billingCode: json['billingCode'] as String,
      description: json['description'] as String,
      vehicleType: json['vehicleType'] as String,
      maxDistanceKm: json['maxDistanceKm'] as int?,
      cargoIncluded: json['cargoIncluded'] as bool,
      dayRate: (json['dayRate'] as num).toDouble(),
    );
  }

  Map<String, dynamic> toJson() => {
        'contractId': contractId,
        'billingCode': billingCode,
        'description': description,
        'vehicleType': vehicleType,
        'maxDistanceKm': maxDistanceKm,
        'cargoIncluded': cargoIncluded,
        'dayRate': dayRate,
      };
}

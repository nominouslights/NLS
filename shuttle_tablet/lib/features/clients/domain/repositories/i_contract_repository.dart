import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/contract.dart';
import '../entities/contract_rate_line.dart';

abstract interface class IContractRepository {
  Future<Either<Failure, List<Contract>>> getContractsByClientId(String clientId);
  Future<Either<Failure, String>> createContract(CreateContractParams params);
  Future<Either<Failure, void>> updateContract(String contractId, UpdateContractParams params);
  Future<Either<Failure, String>> addRateLine(AddRateLineParams params);
  Future<Either<Failure, void>> deleteRateLine(String rateLineId, String clientId);
  Future<Either<Failure, List<ContractRateLine>>> getRateLinesByClientId(String clientId);
}

class CreateContractParams {
  final String clientId;
  final DateTime startDate;
  final DateTime endDate;
  final String? notes;
  final List<RateLineParams> rateLines;

  const CreateContractParams({
    required this.clientId,
    required this.startDate,
    required this.endDate,
    this.notes,
    required this.rateLines,
  });
}

class UpdateContractParams {
  final DateTime startDate;
  final DateTime endDate;
  final String? notes;

  const UpdateContractParams({
    required this.startDate,
    required this.endDate,
    this.notes,
  });
}

class AddRateLineParams {
  final String contractId;
  final String clientId;
  final String billingCode;
  final String description;
  final String vehicleType;
  final int? maxDistanceKm;
  final bool cargoIncluded;
  final double dayRate;

  const AddRateLineParams({
    required this.contractId,
    required this.clientId,
    required this.billingCode,
    required this.description,
    required this.vehicleType,
    this.maxDistanceKm,
    required this.cargoIncluded,
    required this.dayRate,
  });
}

class RateLineParams {
  final String billingCode;
  final String description;
  final String vehicleType;
  final int? maxDistanceKm;
  final bool cargoIncluded;
  final double dayRate;

  const RateLineParams({
    required this.billingCode,
    required this.description,
    required this.vehicleType,
    this.maxDistanceKm,
    required this.cargoIncluded,
    required this.dayRate,
  });
}

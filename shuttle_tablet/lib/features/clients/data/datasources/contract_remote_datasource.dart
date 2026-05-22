import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../../domain/repositories/i_contract_repository.dart';
import '../models/contract_model.dart';
import '../models/contract_rate_line_model.dart';

abstract interface class IContractRemoteDataSource {
  Future<List<ContractModel>> getContractsByClientId(String clientId);
  Future<String> createContract(CreateContractParams params);
  Future<void> updateContract(String contractId, String clientId, UpdateContractParams params);
  Future<String> addRateLine(AddRateLineParams params);
  Future<void> deleteRateLine(String rateLineId, String clientId);
  Future<List<ContractRateLineModel>> getRateLinesByClientId(String clientId);
}

class ContractRemoteDataSource implements IContractRemoteDataSource {
  final Dio _dio;
  const ContractRemoteDataSource(this._dio);

  @override
  Future<List<ContractModel>> getContractsByClientId(String clientId) async {
    try {
      final response = await _dio.get(ApiEndpoints.contractsByClient(clientId));
      final list = response.data as List<dynamic>;
      return list
          .map((e) => ContractModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to load contracts',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<String> createContract(CreateContractParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.contractsByClient(params.clientId),
        data: {
          'startDate': params.startDate.toIso8601String(),
          'renewalDate': params.renewalDate.toIso8601String(),
          'notes': params.notes,
          'rateLines': params.rateLines.map((r) => {
            'billingCode': r.billingCode,
            'description': r.description,
            'vehicleType': r.vehicleType,
            'maxDistanceKm': r.maxDistanceKm,
            'cargoIncluded': r.cargoIncluded,
            'dayRate': r.dayRate,
          }).toList(),
        },
      );
      final data = response.data as Map<String, dynamic>;
      return data['contractId'] as String;
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to create contract',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> updateContract(String contractId, String clientId, UpdateContractParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.contractById(clientId, contractId),
        data: {
          'startDate': params.startDate.toIso8601String(),
          'renewalDate': params.renewalDate.toIso8601String(),
          'notes': params.notes,
        },
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to update contract',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<String> addRateLine(AddRateLineParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.contractRates(params.clientId, params.contractId),
        data: {
          'contractId': params.contractId,
          'billingCode': params.billingCode,
          'description': params.description,
          'vehicleType': params.vehicleType,
          'maxDistanceKm': params.maxDistanceKm,
          'cargoIncluded': params.cargoIncluded,
          'dayRate': params.dayRate,
        },
      );
      final data = response.data as Map<String, dynamic>;
      return data['rateLineId'] as String;
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to add rate line',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> deleteRateLine(String rateLineId, String clientId) async {
    try {
      await _dio.delete(ApiEndpoints.deleteRateLine(clientId, rateLineId));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to delete rate line',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<List<ContractRateLineModel>> getRateLinesByClientId(String clientId) async {
    try {
      final response = await _dio.get(ApiEndpoints.rateLinesByClient(clientId));
      final list = response.data as List<dynamic>;
      return list
          .map((e) => ContractRateLineModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to load rate lines',
        statusCode: e.response?.statusCode,
      );
    }
  }
}

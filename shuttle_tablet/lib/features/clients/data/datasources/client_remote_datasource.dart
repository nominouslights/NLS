import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../../domain/entities/client.dart';
import '../../domain/repositories/i_client_repository.dart';
import '../models/client_model.dart';

abstract interface class IClientRemoteDataSource {
  Future<List<ClientModel>> getClients();
  Future<ClientModel> getClientById(String id);
  Future<String> createClient(CreateClientParams params);
  Future<void> updateClient(String id, UpdateClientParams params);
  Future<void> deleteClient(String id);
}

class ClientRemoteDataSource implements IClientRemoteDataSource {
  final Dio _dio;
  const ClientRemoteDataSource(this._dio);

  @override
  Future<List<ClientModel>> getClients() async {
    try {
      final response = await _dio.get(ApiEndpoints.clients);
      final list = response.data as List<dynamic>;
      return list
          .map((e) => ClientModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to load clients',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<ClientModel> getClientById(String id) async {
    try {
      final response = await _dio.get(ApiEndpoints.clientById(id));
      return ClientModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to load client',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<String> createClient(CreateClientParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.clients,
        data: _clientParamsToJson(params),
      );
      final data = response.data as Map<String, dynamic>;
      return data['id'] as String;
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to create client',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> updateClient(String id, UpdateClientParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.clientById(id),
        data: {
          ..._clientParamsToJson(params),
          'isActive': params.isActive,
        },
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to update client',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> deleteClient(String id) async {
    try {
      await _dio.delete(ApiEndpoints.clientById(id));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to delete client',
        statusCode: e.response?.statusCode,
      );
    }
  }

  Map<String, dynamic> _clientParamsToJson(CreateClientParams params) => {
        'businessName': params.businessName,
        'serviceType': _serviceTypeToString(params.serviceType),
        'primaryContactName': params.primaryContactName,
        'primaryContactTitle': params.primaryContactTitle,
        'phone': params.phone,
        'email': params.email,
        'streetAddress': params.streetAddress,
        'city': params.city,
        'province': params.province,
        'postalCode': params.postalCode,
        'gstHstNumber': params.gstHstNumber,
        'preferredPaymentMethod': params.preferredPaymentMethod,
        'netPaymentTerms': params.netPaymentTerms,
        'complianceNotes': params.complianceNotes,
        'isMinesite': params.isMinesite,
      };

  String _serviceTypeToString(ServiceType t) =>
      t.name[0].toUpperCase() + t.name.substring(1);
}

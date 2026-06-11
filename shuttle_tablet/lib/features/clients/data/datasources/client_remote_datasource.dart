import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../../domain/entities/client.dart';
import '../../domain/entities/client_email_template.dart';
import '../../domain/repositories/i_client_repository.dart';
import '../models/client_model.dart';
import '../models/client_email_template_model.dart';

abstract interface class IClientRemoteDataSource {
  Future<List<ClientModel>> getClients();
  Future<ClientModel> getClientById(String id);
  Future<String> createClient(CreateClientParams params);
  Future<void> updateClient(String id, UpdateClientParams params);
  Future<void> deleteClient(String id);
  Future<List<ClientEmailTemplateModel>> getEmailTemplates(String clientId);
  Future<void> upsertEmailTemplate(
    String clientId,
    ClientEmailTemplateType type,
    String subject,
    String body,
  );
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
        data: _updateClientParamsToJson(params),
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

  @override
  Future<List<ClientEmailTemplateModel>> getEmailTemplates(
      String clientId) async {
    try {
      final response = await _dio.get(ApiEndpoints.clientEmailTemplates(clientId));
      final list = response.data as List<dynamic>;
      return list
          .map((e) =>
              ClientEmailTemplateModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to load email templates',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> upsertEmailTemplate(
    String clientId,
    ClientEmailTemplateType type,
    String subject,
    String body,
  ) async {
    try {
      await _dio.put(
        ApiEndpoints.clientEmailTemplateByType(clientId, type.apiValue),
        data: {'subject': subject, 'body': body},
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      final data = e.response?.data;
      final message = data is Map && data['error'] is String
          ? data['error'] as String
          : (e.message ?? 'Failed to save email template');
      throw ServerException(
        message: message,
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
        'industry': params.industry,
        'projectSite': params.projectSite,
      };

  Map<String, dynamic> _updateClientParamsToJson(UpdateClientParams params) {
    final json = {
      ..._clientParamsToJson(params),
      'isActive': params.isActive,
    };
    if (params.notificationEmails != null) {
      json['notificationEmails'] = params.notificationEmails;
    }
    if (params.tripDepartureArrivalEmails != null) {
      json['tripDepartureArrivalEmails'] = params.tripDepartureArrivalEmails;
    }
    if (params.passengerBookingEmails != null) {
      json['passengerBookingEmails'] = params.passengerBookingEmails;
    }
    return json;
  }

  String _serviceTypeToString(ServiceType t) =>
      t.name[0].toUpperCase() + t.name.substring(1);
}

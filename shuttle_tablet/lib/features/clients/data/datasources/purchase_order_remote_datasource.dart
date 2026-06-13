import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../../domain/repositories/i_purchase_order_repository.dart';
import '../models/purchase_order_model.dart';

abstract interface class IPurchaseOrderRemoteDataSource {
  Future<List<PurchaseOrderModel>> getPurchaseOrdersByClientId(String clientId);
  Future<PurchaseOrderModel> getPurchaseOrderById(String clientId, String id);
  Future<String> createPurchaseOrder(CreatePurchaseOrderParams params);
  Future<void> updatePurchaseOrder(String id, UpdatePurchaseOrderParams params);
}

class PurchaseOrderRemoteDataSource implements IPurchaseOrderRemoteDataSource {
  final Dio _dio;
  const PurchaseOrderRemoteDataSource(this._dio);

  @override
  Future<List<PurchaseOrderModel>> getPurchaseOrdersByClientId(String clientId) async {
    try {
      final response = await _dio.get(ApiEndpoints.purchaseOrdersByClient(clientId));
      final list = response.data as List<dynamic>;
      return list
          .map((e) => PurchaseOrderModel.fromSummaryJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to load purchase orders',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<PurchaseOrderModel> getPurchaseOrderById(String clientId, String id) async {
    try {
      final response = await _dio.get(ApiEndpoints.purchaseOrderById(clientId, id));
      return PurchaseOrderModel.fromDetailJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to load purchase order',
        statusCode: e.response?.statusCode,
      );
    }
  }

  static Map<String, dynamic> _upsertBody({
    required String poNumber,
    required DateTime startDate,
    required String? details,
    required List<String> contractIds,
    required List<PurchaseOrderLineItemParams> lineItems,
  }) =>
      {
        'poNumber': poNumber,
        'startDate': startDate.toUtc().toIso8601String(),
        'details': details,
        'contractIds': contractIds,
        'lineItems': lineItems
            .map((i) => {
                  'description': i.description,
                  'unitRate': i.unitRate,
                  'quantity': i.quantity,
                })
            .toList(),
      };

  @override
  Future<String> createPurchaseOrder(CreatePurchaseOrderParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.purchaseOrdersByClient(params.clientId),
        data: _upsertBody(
          poNumber: params.poNumber,
          startDate: params.startDate,
          details: params.details,
          contractIds: params.contractIds,
          lineItems: params.lineItems,
        ),
      );
      final data = response.data as Map<String, dynamic>;
      final id = data['purchaseOrderId'] ??
          data['PurchaseOrderId'] ??
          data['id'] ??
          data['Id'];
      if (id == null) {
        throw ServerException(
          message: 'Create purchase order succeeded but no id was returned.',
          statusCode: response.statusCode,
        );
      }
      return id.toString();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to create purchase order',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> updatePurchaseOrder(String id, UpdatePurchaseOrderParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.purchaseOrderById(params.clientId, id),
        data: _upsertBody(
          poNumber: params.poNumber,
          startDate: params.startDate,
          details: params.details,
          contractIds: params.contractIds,
          lineItems: params.lineItems,
        ),
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to update purchase order',
        statusCode: e.response?.statusCode,
      );
    }
  }
}

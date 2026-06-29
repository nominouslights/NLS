import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../models/billing_ready_trip_model.dart';
import '../models/invoice_model.dart';

abstract interface class IInvoiceRemoteDataSource {
  Future<List<InvoiceModel>> getByClientId(String clientId, {String? status});
  Future<InvoiceModel> getById(String invoiceId);
  Future<InvoiceModel> create(
    String clientId,
    List<String> tripIds,
    String? notes,
  );
  Future<void> markSent(String invoiceId);
  Future<void> markPaid(String invoiceId);
  Future<void> voidInvoice(String invoiceId);
  Future<List<BillingReadyTripModel>> getBillingReadyTrips(String clientId);
  Future<void> updateBillingRates(
    String clientId, {
    double? oneWayRate,
    double? roundTripRate,
    double? cargoRatePerKg,
  });
}

class InvoiceRemoteDataSource implements IInvoiceRemoteDataSource {
  final Dio _dio;
  const InvoiceRemoteDataSource(this._dio);

  @override
  Future<List<InvoiceModel>> getByClientId(
    String clientId, {
    String? status,
  }) async {
    try {
      final url = status != null
          ? ApiEndpoints.invoicesByClientFiltered(clientId, status)
          : ApiEndpoints.invoicesByClient(clientId);
      final response = await _dio.get(url);
      final list = response.data as List<dynamic>;
      return list
          .map((e) => InvoiceModel.fromSummaryJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to load invoices',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<InvoiceModel> getById(String invoiceId) async {
    try {
      final response = await _dio.get(ApiEndpoints.invoiceById(invoiceId));
      return InvoiceModel.fromDetailJson(
        response.data as Map<String, dynamic>,
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to load invoice',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<InvoiceModel> create(
    String clientId,
    List<String> tripIds,
    String? notes,
  ) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.invoices,
        data: {
          'clientId': clientId,
          'tripIds': tripIds,
          'notes': notes,
        },
      );
      return InvoiceModel.fromDetailJson(
        response.data as Map<String, dynamic>,
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to create invoice',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> markSent(String invoiceId) async {
    try {
      await _dio.put(ApiEndpoints.invoiceSend(invoiceId));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to mark invoice as sent',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> markPaid(String invoiceId) async {
    try {
      await _dio.put(ApiEndpoints.invoicePaid(invoiceId));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to mark invoice as paid',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> voidInvoice(String invoiceId) async {
    try {
      await _dio.put(ApiEndpoints.invoiceVoid(invoiceId));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to void invoice',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<List<BillingReadyTripModel>> getBillingReadyTrips(
    String clientId,
  ) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.billingReadyTrips(clientId),
      );
      final list = response.data as List<dynamic>;
      return list
          .map(
            (e) => BillingReadyTripModel.fromJson(e as Map<String, dynamic>),
          )
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to load billing-ready trips',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> updateBillingRates(
    String clientId, {
    double? oneWayRate,
    double? roundTripRate,
    double? cargoRatePerKg,
  }) async {
    try {
      await _dio.put(
        ApiEndpoints.clientBillingRates(clientId),
        data: {
          'runRateOneWay': oneWayRate,
          'runRateRoundTrip': roundTripRate,
          'cargoRatePerKg': cargoRatePerKg,
        },
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to update billing rates',
        statusCode: e.response?.statusCode,
      );
    }
  }
}

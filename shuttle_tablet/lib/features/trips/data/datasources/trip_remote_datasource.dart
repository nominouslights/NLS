import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../../domain/entities/trip.dart';
import '../../domain/entities/trip_post_report.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../models/trip_model.dart';

abstract interface class ITripRemoteDataSource {
  Future<List<TripModel>> getTrips({
    TripStatus? status,
    String? clientId,
    String? driverId,
  });

  Future<TripModel> getTripById(String id);

  Future<String> createTrip(CreateTripParams params);

  Future<void> updateTrip(String id, UpdateTripParams params);

  Future<void> deleteTrip(String id);

  Future<void> assignDriver(String tripId, AssignDriverParams params);

  Future<void> dispatchTrip(String tripId);

  Future<void> updateTripStatus(String tripId, TripStatus status);

  Future<void> submitPreInspection(
      String tripId, SubmitPreInspectionParams params);

  Future<void> submitPostReport(String tripId, SubmitPostReportParams params);
}

class TripRemoteDataSource implements ITripRemoteDataSource {
  final Dio _dio;
  const TripRemoteDataSource(this._dio);

  @override
  Future<List<TripModel>> getTrips({
    TripStatus? status,
    String? clientId,
    String? driverId,
  }) async {
    try {
      final queryParams = <String, dynamic>{};
      if (status != null) queryParams['status'] = _statusToString(status);
      if (clientId != null) queryParams['clientId'] = clientId;
      if (driverId != null) queryParams['driverId'] = driverId;

      final response = await _dio.get(
        ApiEndpoints.trips,
        queryParameters: queryParams.isEmpty ? null : queryParams,
      );
      final list = response.data as List<dynamic>;
      return list
          .map((e) => TripModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to load trips',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<TripModel> getTripById(String id) async {
    try {
      final response = await _dio.get(ApiEndpoints.tripById(id));
      return TripModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to load trip',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<String> createTrip(CreateTripParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.trips,
        data: _createTripToJson(params),
      );
      final data = response.data as Map<String, dynamic>;
      return data['id'] as String;
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to create trip',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> updateTrip(String id, UpdateTripParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.tripById(id),
        data: _updateTripToJson(params),
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to update trip',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> deleteTrip(String id) async {
    try {
      await _dio.delete(ApiEndpoints.tripById(id));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to delete trip',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> assignDriver(String tripId, AssignDriverParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.tripAssignDriver(tripId),
        data: {
          'driverId': params.driverId,
          'vehicleType': params.vehicleType,
        },
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to assign driver',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> dispatchTrip(String tripId) async {
    try {
      await _dio.post(ApiEndpoints.tripDispatch(tripId));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to dispatch trip',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> updateTripStatus(String tripId, TripStatus status) async {
    try {
      await _dio.put(
        ApiEndpoints.tripStatus(tripId),
        data: {'status': _statusToString(status)},
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to update trip status',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> submitPreInspection(
      String tripId, SubmitPreInspectionParams params) async {
    try {
      await _dio.post(
        ApiEndpoints.tripPreInspection(tripId),
        data: {
          'odometerStart': params.odometerStart,
          'items': params.items
              .map((i) => {
                    'itemName': i.itemName,
                    'passed': i.passed,
                    'notes': i.notes,
                  })
              .toList(),
        },
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to submit pre-inspection',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> submitPostReport(
      String tripId, SubmitPostReportParams params) async {
    try {
      await _dio.post(
        ApiEndpoints.tripPostReport(tripId),
        data: {
          'odometerEnd': params.odometerEnd,
          'fuelAddedLitres': params.fuelAddedLitres,
          'fuelCostDollars': params.fuelCostDollars,
          'hasIncident': params.hasIncident,
          'incidentType': _incidentTypeToString(params.incidentType),
          'incidentDescription': params.incidentDescription,
          'additionalNotes': params.additionalNotes,
          'isReadyToInvoice': params.isReadyToInvoice,
        },
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to submit post-trip report',
        statusCode: e.response?.statusCode,
      );
    }
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  static String _statusToString(TripStatus status) {
    const map = {
      TripStatus.scheduled: 'Scheduled',
      TripStatus.dispatched: 'Dispatched',
      TripStatus.enRoute: 'EnRoute',
      TripStatus.completed: 'Completed',
      TripStatus.cancelled: 'Cancelled',
    };
    return map[status]!;
  }

  static String? _incidentTypeToString(IncidentType? type) {
    if (type == null) return null;
    const map = {
      IncidentType.delay: 'Delay',
      IncidentType.passengerNoShow: 'PassengerNoShow',
      IncidentType.vehicleIssue: 'VehicleIssue',
      IncidentType.cargoDamage: 'CargoDamage',
      IncidentType.accident: 'Accident',
    };
    return map[type];
  }

  static Map<String, dynamic> _createTripToJson(CreateTripParams p) => {
        'clientId': p.clientId,
        'purchaseOrderNumber': p.purchaseOrderNumber,
        'vehicleType': p.vehicleType,
        'scheduledAt': p.scheduledAt.toIso8601String(),
        'notes': p.notes,
        'stops': p.stops
            .map((s) => {
                  'sequenceOrder': s.sequenceOrder,
                  'locationName': s.locationName,
                  'address': s.address,
                })
            .toList(),
      };

  static Map<String, dynamic> _updateTripToJson(UpdateTripParams p) => {
        'purchaseOrderNumber': p.purchaseOrderNumber,
        'vehicleType': p.vehicleType,
        'scheduledAt': p.scheduledAt.toIso8601String(),
        'notes': p.notes,
        'stops': p.stops
            .map((s) => {
                  'sequenceOrder': s.sequenceOrder,
                  'locationName': s.locationName,
                  'address': s.address,
                })
            .toList(),
      };
}

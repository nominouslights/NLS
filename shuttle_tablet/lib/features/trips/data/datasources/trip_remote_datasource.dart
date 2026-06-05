import 'package:dio/dio.dart';
import '../../../../core/debug/agent_log.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../../domain/entities/trip.dart';
import '../../domain/entities/trip_cargo_item.dart';
import '../../domain/entities/trip_passenger.dart';
import '../../domain/entities/trip_post_report.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../models/trip_model.dart';
import '../models/trip_passenger_model.dart';

abstract interface class ITripRemoteDataSource {
  Future<List<TripModel>> getTrips({
    TripStatus? status,
    String? clientId,
    String? driverId,
    String? vehicleId,
    TripServiceType? serviceType,
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

  Future<List<TripPassengerModel>> getPassengers(String tripId);

  Future<String> addPassenger(AddPassengerParams params);

  Future<void> removePassenger(String tripId, String passengerId);

  Future<void> updatePassengerPaymentStatus(
      UpdatePassengerPaymentStatusParams params);

  Future<String> addCargoItem(AddCargoItemParams params);

  Future<void> removeCargoItem(String tripId, String cargoItemId);
}

class TripRemoteDataSource implements ITripRemoteDataSource {
  final Dio _dio;
  const TripRemoteDataSource(this._dio);

  @override
  Future<List<TripModel>> getTrips({
    TripStatus? status,
    String? clientId,
    String? driverId,
    String? vehicleId,
    TripServiceType? serviceType,
  }) async {
    try {
      final queryParams = <String, dynamic>{};
      if (status != null) queryParams['status'] = _statusToString(status);
      if (clientId != null) queryParams['clientId'] = clientId;
      if (driverId != null) queryParams['driverId'] = driverId;
      if (vehicleId != null) queryParams['vehicleId'] = vehicleId;
      if (serviceType != null) {
        queryParams['serviceType'] = _serviceTypeToString(serviceType);
      }

      final response = await _dio.get(
        ApiEndpoints.trips,
        queryParameters: queryParams.isEmpty ? null : queryParams,
      );
      final list = response.data as List<dynamic>;
      return list
          .map((e) => TripModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      // #region agent log
      agentLog(
        location: 'trip_remote_datasource.dart:getTrips',
        message: 'trips list failed',
        hypothesisId: 'A',
        data: {
          'statusCode': e.response?.statusCode,
          'error': e.message,
          'body': e.response?.data?.toString(),
        },
      );
      // #endregion
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
      // #region agent log
      final data = response.data as Map<String, dynamic>;
      agentLog(
        location: 'trip_remote_datasource.dart:getTripById',
        message: 'trip detail response',
        hypothesisId: 'A',
        data: {
          'tripId': id,
          'statusCode': response.statusCode,
          'hasCargoItems': data.containsKey('cargoItems'),
          'hasCargoItemsPascal': data.containsKey('CargoItems'),
          'cargoItemsType': data['cargoItems']?.runtimeType.toString(),
        },
      );
      // #endregion
      return TripModel.fromJson(data);
    } on DioException catch (e) {
      // #region agent log
      agentLog(
        location: 'trip_remote_datasource.dart:getTripById',
        message: 'trip detail failed',
        hypothesisId: 'A',
        data: {
          'tripId': id,
          'statusCode': e.response?.statusCode,
          'error': e.message,
          'body': e.response?.data?.toString(),
        },
      );
      // #endregion
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

  @override
  Future<List<TripPassengerModel>> getPassengers(String tripId) async {
    try {
      final response = await _dio.get(ApiEndpoints.tripPassengers(tripId));
      final list = response.data as List<dynamic>;
      return list
          .map((e) => TripPassengerModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to load passengers',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<String> addPassenger(AddPassengerParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.tripPassengers(params.tripId),
        data: {
          'name': params.name,
          'contactInfo': params.contactInfo,
          'seatNumber': params.seatNumber,
          'paymentStatus': _paymentStatusToString(params.paymentStatus),
          'phone': params.phone,
          'email': params.email,
          'isAddedAfterDeparture': params.isAddedAfterDeparture,
        },
      );
      final data = response.data as Map<String, dynamic>;
      return data['passengerId'] as String;
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to add passenger',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> removePassenger(String tripId, String passengerId) async {
    try {
      await _dio.delete(ApiEndpoints.tripPassengerById(tripId, passengerId));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to remove passenger',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> updatePassengerPaymentStatus(
      UpdatePassengerPaymentStatusParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.tripPassengerPaymentStatus(
            params.tripId, params.passengerId),
        data: {'paymentStatus': _paymentStatusToString(params.paymentStatus)},
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to update payment status',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<String> addCargoItem(AddCargoItemParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.tripCargo(params.tripId),
        data: {
          'cargoType': _cargoTypeToString(params.cargoType),
          'description': params.description,
          'quantity': params.quantity,
        },
      );
      final data = response.data as Map<String, dynamic>;
      return data['cargoItemId'] as String;
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to add cargo',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> removeCargoItem(String tripId, String cargoItemId) async {
    try {
      await _dio.delete(ApiEndpoints.tripCargoById(tripId, cargoItemId));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to remove cargo',
        statusCode: e.response?.statusCode,
      );
    }
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  static String _cargoTypeToString(TripCargoType type) {
    const map = {
      TripCargoType.box: 'Box',
      TripCargoType.pallet: 'Pallet',
    };
    return map[type]!;
  }

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

  static String _serviceTypeToString(TripServiceType type) {
    const map = {
      TripServiceType.charter: 'Charter',
      TripServiceType.community: 'Community',
    };
    return map[type]!;
  }

  static String _paymentStatusToString(PassengerPaymentStatus status) {
    const map = {
      PassengerPaymentStatus.tentative: 'Tentative',
      PassengerPaymentStatus.awaitingPayment: 'AwaitingPayment',
      PassengerPaymentStatus.confirmed: 'Confirmed',
      PassengerPaymentStatus.released: 'Released',
      PassengerPaymentStatus.cancelled: 'Cancelled',
      PassengerPaymentStatus.pending: 'Tentative',
      PassengerPaymentStatus.paid: 'Confirmed',
    };
    return map[status]!;
  }

  static Map<String, dynamic> _createTripToJson(CreateTripParams p) => {
        'serviceType': _serviceTypeToString(p.serviceType),
        'clientId': p.clientId,
        'vehicleId': p.vehicleId,
        'purchaseOrderNumber': p.purchaseOrderNumber,
        'vehicleType': p.vehicleType,
        'scheduledAt': p.scheduledAt.toUtc().toIso8601String(),
        'notes': p.notes,
        'stops': p.stops
            .map((s) => {
                  'sequenceOrder': s.sequenceOrder,
                  'locationName': s.locationName,
                  'address': s.address,
                })
            .toList(),
        'seatCapacity': p.seatCapacity,
        'pricePerSeat': p.pricePerSeat,
      };

  static Map<String, dynamic> _updateTripToJson(UpdateTripParams p) => {
        'vehicleId': p.vehicleId,
        'purchaseOrderNumber': p.purchaseOrderNumber,
        'vehicleType': p.vehicleType,
        'scheduledAt': p.scheduledAt.toUtc().toIso8601String(),
        'notes': p.notes,
        'stops': p.stops
            .map((s) => {
                  'sequenceOrder': s.sequenceOrder,
                  'locationName': s.locationName,
                  'address': s.address,
                })
            .toList(),
        'seatCapacity': p.seatCapacity,
        'pricePerSeat': p.pricePerSeat,
      };
}

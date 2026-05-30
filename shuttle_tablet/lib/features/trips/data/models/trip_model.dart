import '../../domain/entities/trip.dart';
import 'trip_passenger_model.dart';
import 'trip_stop_model.dart';
import 'trip_pre_inspection_model.dart';
import 'trip_post_report_model.dart';

class TripModel extends Trip {
  const TripModel({
    required super.id,
    super.clientId,
    super.vehicleId,
    super.driverId,
    super.serviceType = TripServiceType.charter,
    super.purchaseOrderNumber,
    super.vehicleType,
    required super.scheduledAt,
    required super.status,
    super.notes,
    required super.createdAt,
    super.seatCapacity,
    super.pricePerSeat,
    super.stops = const [],
    super.passengers = const [],
    super.preInspection,
    super.postReport,
  });

  factory TripModel.fromJson(Map<String, dynamic> json) {
    final stopsJson = json['stops'] as List<dynamic>? ?? [];
    final passengersJson = json['passengers'] as List<dynamic>? ?? [];
    final preInspectionJson = json['preInspection'] as Map<String, dynamic>?;
    final postReportJson = json['postReport'] as Map<String, dynamic>?;

    return TripModel(
      id: json['id'] as String,
      clientId: json['clientId'] as String?,
      vehicleId: json['vehicleId'] as String?,
      driverId: json['driverId'] as String?,
      serviceType: _parseServiceType(json['serviceType'] as String? ?? ''),
      purchaseOrderNumber: json['purchaseOrderNumber'] as String?,
      vehicleType: json['vehicleType'] as String?,
      scheduledAt:
          DateTime.tryParse(json['scheduledAt'] as String? ?? '') ??
              DateTime.now(),
      status: _parseStatus(json['status'] as String? ?? ''),
      notes: json['notes'] as String?,
      createdAt:
          DateTime.tryParse(json['createdAt'] as String? ?? '') ??
              DateTime.now(),
      seatCapacity: json['seatCapacity'] as int?,
      pricePerSeat: (json['pricePerSeat'] as num?)?.toDouble(),
      stops: stopsJson
          .map((e) => TripStopModel.fromJson(e as Map<String, dynamic>))
          .toList(),
      passengers: passengersJson
          .map((e) => TripPassengerModel.fromJson(e as Map<String, dynamic>))
          .toList(),
      preInspection: preInspectionJson != null
          ? TripPreInspectionModel.fromJson(preInspectionJson)
          : null,
      postReport: postReportJson != null
          ? TripPostReportModel.fromJson(postReportJson)
          : null,
    );
  }

  static TripServiceType _parseServiceType(String value) {
    const map = {
      'Charter': TripServiceType.charter,
      'Community': TripServiceType.community,
    };
    return map[value] ?? TripServiceType.charter;
  }

  static TripStatus _parseStatus(String value) {
    const map = {
      'Scheduled': TripStatus.scheduled,
      'Dispatched': TripStatus.dispatched,
      'EnRoute': TripStatus.enRoute,
      'Completed': TripStatus.completed,
      'Cancelled': TripStatus.cancelled,
    };
    return map[value] ?? TripStatus.scheduled;
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

  static String _serviceTypeToString(TripServiceType type) {
    const map = {
      TripServiceType.charter: 'Charter',
      TripServiceType.community: 'Community',
    };
    return map[type]!;
  }

  Map<String, dynamic> toJson() => {
        'serviceType': _serviceTypeToString(serviceType),
        'clientId': clientId,
        'vehicleId': vehicleId,
        'driverId': driverId,
        'purchaseOrderNumber': purchaseOrderNumber,
        'vehicleType': vehicleType,
        'scheduledAt': scheduledAt.toIso8601String(),
        'status': _statusToString(status),
        'notes': notes,
        'seatCapacity': seatCapacity,
        'pricePerSeat': pricePerSeat,
        'stops': stops.map((s) => (s as TripStopModel).toJson()).toList(),
      };
}

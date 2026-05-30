import 'package:equatable/equatable.dart';
import 'trip_passenger.dart';
import 'trip_stop.dart';
import 'trip_pre_inspection.dart';
import 'trip_post_report.dart';

enum TripStatus { scheduled, dispatched, enRoute, completed, cancelled }

enum TripServiceType { charter, community }

class Trip extends Equatable {
  final String id;
  final String? clientId;
  final String? vehicleId;
  final String? driverId;
  final TripServiceType serviceType;
  final String? purchaseOrderNumber;
  final String? vehicleType;
  final DateTime scheduledAt;
  final TripStatus status;
  final String? notes;
  final DateTime createdAt;
  final int? seatCapacity;
  final double? pricePerSeat;
  final List<TripStop> stops;
  final List<TripPassenger> passengers;
  final TripPreInspection? preInspection;
  final TripPostReport? postReport;

  const Trip({
    required this.id,
    this.clientId,
    this.vehicleId,
    this.driverId,
    this.serviceType = TripServiceType.charter,
    this.purchaseOrderNumber,
    this.vehicleType,
    required this.scheduledAt,
    required this.status,
    this.notes,
    required this.createdAt,
    this.seatCapacity,
    this.pricePerSeat,
    this.stops = const [],
    this.passengers = const [],
    this.preInspection,
    this.postReport,
  });

  String? get firstStopLocation =>
      stops.isNotEmpty ? stops.first.locationName : null;

  String? get lastStopLocation =>
      stops.length > 1 ? stops.last.locationName : null;

  @override
  List<Object?> get props => [
        id,
        clientId,
        vehicleId,
        driverId,
        serviceType,
        purchaseOrderNumber,
        vehicleType,
        scheduledAt,
        status,
        notes,
        createdAt,
        seatCapacity,
        pricePerSeat,
        stops,
        passengers,
        preInspection,
        postReport,
      ];
}

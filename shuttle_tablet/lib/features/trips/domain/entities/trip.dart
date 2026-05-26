import 'package:equatable/equatable.dart';
import 'trip_stop.dart';
import 'trip_pre_inspection.dart';
import 'trip_post_report.dart';

enum TripStatus { scheduled, dispatched, enRoute, completed, cancelled }

class Trip extends Equatable {
  final String id;
  final String clientId;
  final String? driverId;
  final String? purchaseOrderNumber;
  final String? vehicleType;
  final DateTime scheduledAt;
  final TripStatus status;
  final String? notes;
  final DateTime createdAt;
  final List<TripStop> stops;
  final TripPreInspection? preInspection;
  final TripPostReport? postReport;

  const Trip({
    required this.id,
    required this.clientId,
    this.driverId,
    this.purchaseOrderNumber,
    this.vehicleType,
    required this.scheduledAt,
    required this.status,
    this.notes,
    required this.createdAt,
    this.stops = const [],
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
        driverId,
        purchaseOrderNumber,
        vehicleType,
        scheduledAt,
        status,
        notes,
        createdAt,
        stops,
        preInspection,
        postReport,
      ];
}

import 'package:equatable/equatable.dart';
import 'trip_cargo_item.dart';
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
  final String? purchaseOrderId;
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
  final List<TripCargoItem> cargoItems;
  final TripPreInspection? preInspection;
  final TripPostReport? postReport;
  final bool isDeadhead;
  final bool isDeadheadBillable;

  const Trip({
    required this.id,
    this.clientId,
    this.vehicleId,
    this.driverId,
    this.serviceType = TripServiceType.charter,
    this.purchaseOrderId,
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
    this.cargoItems = const [],
    this.preInspection,
    this.postReport,
    this.isDeadhead = false,
    this.isDeadheadBillable = false,
  });

  bool get hasManifest =>
      passengers.isNotEmpty || cargoItems.isNotEmpty;

  bool get canDispatch => isDeadhead || hasManifest;

  static const dispatchManifestMessage =
      'Add at least one passenger or cargo item, or mark the trip as a deadhead trip before dispatching.';

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
        purchaseOrderId,
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
        cargoItems,
        preInspection,
        postReport,
        isDeadhead,
        isDeadheadBillable,
      ];
}

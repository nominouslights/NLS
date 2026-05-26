import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/trip.dart';
import '../entities/trip_post_report.dart';

abstract interface class ITripRepository {
  Future<Either<Failure, List<Trip>>> getTrips({
    TripStatus? status,
    String? clientId,
    String? driverId,
  });

  Future<Either<Failure, Trip>> getTripById(String id);

  Future<Either<Failure, String>> createTrip(CreateTripParams params);

  Future<Either<Failure, void>> updateTrip(String id, UpdateTripParams params);

  Future<Either<Failure, void>> deleteTrip(String id);

  Future<Either<Failure, void>> assignDriver(
      String tripId, AssignDriverParams params);

  Future<Either<Failure, void>> dispatchTrip(String tripId);

  Future<Either<Failure, void>> updateTripStatus(
      String tripId, TripStatus status);

  Future<Either<Failure, void>> submitPreInspection(
      String tripId, SubmitPreInspectionParams params);

  Future<Either<Failure, void>> submitPostReport(
      String tripId, SubmitPostReportParams params);
}

// ── Shared ──────────────────────────────────────────────────────────────────

class StopParams {
  final int sequenceOrder;
  final String locationName;
  final String? address;

  const StopParams({
    required this.sequenceOrder,
    required this.locationName,
    this.address,
  });
}

class InspectionItemParams {
  final String itemName;
  final bool passed;
  final String? notes;

  const InspectionItemParams({
    required this.itemName,
    required this.passed,
    this.notes,
  });
}

// ── Trip CRUD ────────────────────────────────────────────────────────────────

class CreateTripParams {
  final String clientId;
  final String? purchaseOrderNumber;
  final String? vehicleType;
  final DateTime scheduledAt;
  final String? notes;
  final List<StopParams> stops;

  const CreateTripParams({
    required this.clientId,
    this.purchaseOrderNumber,
    this.vehicleType,
    required this.scheduledAt,
    this.notes,
    required this.stops,
  });
}

class UpdateTripParams {
  final String? purchaseOrderNumber;
  final String? vehicleType;
  final DateTime scheduledAt;
  final String? notes;
  final List<StopParams> stops;

  const UpdateTripParams({
    this.purchaseOrderNumber,
    this.vehicleType,
    required this.scheduledAt,
    this.notes,
    required this.stops,
  });
}

class AssignDriverParams {
  final String driverId;
  final String? vehicleType;

  const AssignDriverParams({required this.driverId, this.vehicleType});
}

// ── Inspection ───────────────────────────────────────────────────────────────

class SubmitPreInspectionParams {
  final int odometerStart;
  final List<InspectionItemParams> items;

  const SubmitPreInspectionParams({
    required this.odometerStart,
    required this.items,
  });
}

class SubmitPostReportParams {
  final int odometerEnd;
  final double? fuelAddedLitres;
  final double? fuelCostDollars;
  final bool hasIncident;
  final IncidentType? incidentType;
  final String? incidentDescription;
  final String? additionalNotes;
  final bool isReadyToInvoice;

  const SubmitPostReportParams({
    required this.odometerEnd,
    this.fuelAddedLitres,
    this.fuelCostDollars,
    required this.hasIncident,
    this.incidentType,
    this.incidentDescription,
    this.additionalNotes,
    required this.isReadyToInvoice,
  });
}

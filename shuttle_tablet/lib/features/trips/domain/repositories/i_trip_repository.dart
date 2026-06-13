import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/trip.dart';
import '../entities/trip_cargo_item.dart';
import '../entities/trip_inspection_item.dart';
import '../entities/trip_passenger.dart';
import '../entities/trip_pre_inspection.dart';
import '../entities/trip_post_report.dart';

abstract interface class ITripRepository {
  Future<Either<Failure, List<Trip>>> getTrips({
    TripStatus? status,
    String? clientId,
    String? driverId,
    String? vehicleId,
    TripServiceType? serviceType,
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

  Future<Either<Failure, List<TripPassenger>>> getPassengers(String tripId);

  Future<Either<Failure, String>> addPassenger(AddPassengerParams params);

  Future<Either<Failure, void>> removePassenger(
      String tripId, String passengerId);

  Future<Either<Failure, void>> updatePassengerPaymentStatus(
      UpdatePassengerPaymentStatusParams params);

  Future<Either<Failure, void>> updatePassengerBoardingStatus(
      UpdatePassengerBoardingStatusParams params);

  Future<Either<Failure, void>> sendPassengerConfirmation(
      SendPassengerConfirmationParams params);

  Future<Either<Failure, void>> sendStopUpdate(SendStopUpdateParams params);

  Future<Either<Failure, String>> addCargoItem(AddCargoItemParams params);

  Future<Either<Failure, void>> removeCargoItem(
      String tripId, String cargoItemId);
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
  final InspectionCategory category;
  final bool passed;
  final String? notes;

  const InspectionItemParams({
    required this.itemName,
    required this.category,
    required this.passed,
    this.notes,
  });
}

// ── Trip CRUD ────────────────────────────────────────────────────────────────

class CreateTripParams {
  final TripServiceType serviceType;
  final String? clientId;
  final String? vehicleId;
  final String? purchaseOrderId;
  final String? purchaseOrderNumber;
  final String? vehicleType;
  final DateTime scheduledAt;
  final String? notes;
  final List<StopParams> stops;
  final int? seatCapacity;
  final double? pricePerSeat;
  final bool isDeadhead;
  final bool isDeadheadBillable;

  const CreateTripParams({
    this.serviceType = TripServiceType.charter,
    this.clientId,
    this.vehicleId,
    this.purchaseOrderId,
    this.purchaseOrderNumber,
    this.vehicleType,
    required this.scheduledAt,
    this.notes,
    required this.stops,
    this.seatCapacity,
    this.pricePerSeat,
    this.isDeadhead = false,
    this.isDeadheadBillable = false,
  });
}

class UpdateTripParams {
  final String? vehicleId;
  final String? purchaseOrderId;
  final String? purchaseOrderNumber;
  final String? vehicleType;
  final DateTime scheduledAt;
  final String? notes;
  final List<StopParams> stops;
  final int? seatCapacity;
  final double? pricePerSeat;
  final bool isDeadhead;
  final bool isDeadheadBillable;

  const UpdateTripParams({
    this.vehicleId,
    this.purchaseOrderId,
    this.purchaseOrderNumber,
    this.vehicleType,
    required this.scheduledAt,
    this.notes,
    required this.stops,
    this.seatCapacity,
    this.pricePerSeat,
    this.isDeadhead = false,
    this.isDeadheadBillable = false,
  });
}

class AddPassengerParams {
  final String tripId;
  final String name;
  final String? contactInfo;
  final int? seatNumber;
  final PassengerPaymentStatus paymentStatus;
  final String? phone;
  final String? email;
  final bool isAddedAfterDeparture;

  const AddPassengerParams({
    required this.tripId,
    required this.name,
    this.contactInfo,
    this.seatNumber,
    this.paymentStatus = PassengerPaymentStatus.tentative,
    this.phone,
    this.email,
    this.isAddedAfterDeparture = false,
  });
}

class UpdatePassengerPaymentStatusParams {
  final String tripId;
  final String passengerId;
  final PassengerPaymentStatus paymentStatus;

  const UpdatePassengerPaymentStatusParams({
    required this.tripId,
    required this.passengerId,
    required this.paymentStatus,
  });
}

class UpdatePassengerBoardingStatusParams {
  final String tripId;
  final String passengerId;
  final PassengerBoardingStatus boardingStatus;

  const UpdatePassengerBoardingStatusParams({
    required this.tripId,
    required this.passengerId,
    required this.boardingStatus,
  });
}

enum ConfirmationDirection { outbound, inbound }

class SendPassengerConfirmationParams {
  final String tripId;
  final String passengerId;
  final ConfirmationDirection direction;

  const SendPassengerConfirmationParams({
    required this.tripId,
    required this.passengerId,
    required this.direction,
  });

  String get directionValue =>
      direction == ConfirmationDirection.inbound ? 'Inbound' : 'Outbound';
}

class SendStopUpdateParams {
  final String tripId;
  final String? stopId;
  final String? status;

  const SendStopUpdateParams({
    required this.tripId,
    this.stopId,
    this.status,
  });
}

class AddCargoItemParams {
  final String tripId;
  final TripCargoType cargoType;
  final String? description;
  final int quantity;
  final double? weightKg;
  final double? charge;
  final bool isHazmat;
  final bool isSecured;

  const AddCargoItemParams({
    required this.tripId,
    required this.cargoType,
    this.description,
    this.quantity = 1,
    this.weightKg,
    this.charge,
    this.isHazmat = false,
    this.isSecured = false,
  });
}

class RemoveCargoItemParams {
  final String tripId;
  final String cargoItemId;

  const RemoveCargoItemParams({
    required this.tripId,
    required this.cargoItemId,
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
  final FuelLevel fuelLevel;
  final String? weatherType;
  final String? temperature;
  final String? roadConditions;
  final String? visibility;
  final String? roadAdvisories;
  final DateTime? weatherPulledAt;
  final List<InspectionItemParams> items;

  const SubmitPreInspectionParams({
    required this.odometerStart,
    this.fuelLevel = FuelLevel.full,
    this.weatherType,
    this.temperature,
    this.roadConditions,
    this.visibility,
    this.roadAdvisories,
    this.weatherPulledAt,
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
  final bool exteriorNoNewDamage;
  final bool interiorCleanedAndChecked;
  final bool passengersDisembarkedSafely;
  final bool allCargoDeliveredAndAccounted;
  final bool vehicleSecuredAndPluggedIn;
  final bool keysReturnedAndSecured;

  const SubmitPostReportParams({
    required this.odometerEnd,
    this.fuelAddedLitres,
    this.fuelCostDollars,
    required this.hasIncident,
    this.incidentType,
    this.incidentDescription,
    this.additionalNotes,
    required this.isReadyToInvoice,
    this.exteriorNoNewDamage = false,
    this.interiorCleanedAndChecked = false,
    this.passengersDisembarkedSafely = false,
    this.allCargoDeliveredAndAccounted = false,
    this.vehicleSecuredAndPluggedIn = false,
    this.keysReturnedAndSecured = false,
  });
}

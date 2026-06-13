import 'dart:typed_data';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/vehicle.dart';
import '../entities/vehicle_fuel_entry.dart';
import '../entities/vehicle_odometer_entry.dart';

// ── Repository Interface ───────────────────────────────────────────────────────

abstract interface class IVehicleRepository {
  Future<Either<Failure, List<Vehicle>>> getVehicles();
  Future<Either<Failure, Vehicle>> getVehicleById(String id);
  Future<Either<Failure, String>> createVehicle(CreateVehicleParams params);
  Future<Either<Failure, void>> updateVehicle(String id, UpdateVehicleParams params);
  Future<Either<Failure, void>> deleteVehicle(String id);
  Future<Either<Failure, void>> setVehicleStatus(String id, String status, String? statusNote);
  Future<Either<Failure, void>> setVehicleOutOfService(String id, String reason);
  Future<Either<Failure, void>> updateOdometer(String id, int newOdometerKm);
  Future<Either<Failure, String>> addServiceRecord(String vehicleId, AddServiceRecordParams params);
  Future<Either<Failure, void>> updateServiceRecord(String vehicleId, String recordId, AddServiceRecordParams params);
  Future<Either<Failure, void>> completeServiceRecord(String vehicleId, String recordId, CompleteServiceRecordParams params);
  Future<Either<Failure, void>> deleteServiceRecord(String vehicleId, String recordId);
  Future<Either<Failure, String>> addInspectionRecord(String vehicleId, AddInspectionRecordParams params);
  Future<Either<Failure, void>> updateInspectionRecord(String vehicleId, String recordId, AddInspectionRecordParams params);
  Future<Either<Failure, void>> deleteInspectionRecord(String vehicleId, String recordId);
  Future<Either<Failure, List<VehicleFuelEntry>>> getFuelEntries(String vehicleId);
  Future<Either<Failure, String>> addFuelEntry(String vehicleId, AddFuelEntryParams params);
  Future<Either<Failure, void>> deleteFuelEntry(String vehicleId, String entryId);
  Future<Either<Failure, Uint8List>> getFuelReceipt(String vehicleId, String entryId);
  Future<Either<Failure, List<VehicleOdometerEntry>>> getOdometerHistory(String vehicleId);
}

// ── Params ────────────────────────────────────────────────────────────────────

class CreateVehicleParams {
  final String unitCode;
  final String vin;
  final String make;
  final String model;
  final int year;
  final String color;
  final String licensePlate;
  final String province;
  final String vehicleType;
  final int passengerCapacity;
  final int currentOdometerKm;
  final DateTime acquisitionDate;
  final DateTime? registrationExpiry;
  final String? insuranceProvider;
  final String? insurancePolicyNumber;
  final DateTime? insuranceExpiry;
  final String? notes;

  const CreateVehicleParams({
    required this.unitCode,
    required this.vin,
    required this.make,
    required this.model,
    required this.year,
    required this.color,
    required this.licensePlate,
    required this.province,
    required this.vehicleType,
    required this.passengerCapacity,
    required this.currentOdometerKm,
    required this.acquisitionDate,
    this.registrationExpiry,
    this.insuranceProvider,
    this.insurancePolicyNumber,
    this.insuranceExpiry,
    this.notes,
  });
}

class UpdateVehicleParams extends CreateVehicleParams {
  final bool isActive;

  const UpdateVehicleParams({
    required super.unitCode,
    required super.vin,
    required super.make,
    required super.model,
    required super.year,
    required super.color,
    required super.licensePlate,
    required super.province,
    required super.vehicleType,
    required super.passengerCapacity,
    required super.currentOdometerKm,
    required super.acquisitionDate,
    super.registrationExpiry,
    super.insuranceProvider,
    super.insurancePolicyNumber,
    super.insuranceExpiry,
    super.notes,
    required this.isActive,
  });
}

class AddServiceRecordParams {
  final String serviceCategory;
  final String? fluidType;
  final String title;
  final String? description;
  final bool isPlanned;
  final String serviceStatus;
  final String priority;
  final DateTime? scheduledDate;
  final int? odometerAtService;
  final double? estimatedCostDollars;
  final String? serviceProvider;
  final String? technicianName;
  final String? partsNotes;
  final bool isWarrantyWork;
  final DateTime? nextServiceDueDateUtc;
  final int? nextServiceDueOdometerKm;

  const AddServiceRecordParams({
    required this.serviceCategory,
    this.fluidType,
    required this.title,
    this.description,
    required this.isPlanned,
    required this.serviceStatus,
    required this.priority,
    this.scheduledDate,
    this.odometerAtService,
    this.estimatedCostDollars,
    this.serviceProvider,
    this.technicianName,
    this.partsNotes,
    required this.isWarrantyWork,
    this.nextServiceDueDateUtc,
    this.nextServiceDueOdometerKm,
  });
}

class CompleteServiceRecordParams {
  final DateTime completedDate;
  final double? actualCostDollars;
  final int? odometerAtService;

  const CompleteServiceRecordParams({
    required this.completedDate,
    this.actualCostDollars,
    this.odometerAtService,
  });
}

class AddInspectionRecordParams {
  final String inspectionType;
  final DateTime inspectedAt;
  final DateTime? expiresAt;
  final String? inspectorName;
  final String? inspectionFacility;
  final String? certificateNumber;
  final String inspectionResult;
  final String? deficienciesNotes;
  final String? correctiveActionNotes;
  final double? costDollars;

  const AddInspectionRecordParams({
    required this.inspectionType,
    required this.inspectedAt,
    this.expiresAt,
    this.inspectorName,
    this.inspectionFacility,
    this.certificateNumber,
    required this.inspectionResult,
    this.deficienciesNotes,
    this.correctiveActionNotes,
    this.costDollars,
  });
}

class AddFuelEntryParams {
  final DateTime fuelledAt;
  final double fuelLitres;
  final double totalCostDollars;
  final int? odometerAtFuelling;
  final String? notes;
  final Uint8List? receiptPhotoBytes;
  final String? receiptPhotoFileName;
  final String? receiptPhotoContentType;

  const AddFuelEntryParams({
    required this.fuelledAt,
    required this.fuelLitres,
    required this.totalCostDollars,
    this.odometerAtFuelling,
    this.notes,
    this.receiptPhotoBytes,
    this.receiptPhotoFileName,
    this.receiptPhotoContentType,
  });
}

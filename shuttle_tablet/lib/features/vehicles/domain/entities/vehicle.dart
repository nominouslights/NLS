import 'package:equatable/equatable.dart';
import 'vehicle_service_record.dart';
import 'vehicle_inspection_record.dart';

class Vehicle extends Equatable {
  final String id;
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
  final String status;
  final String? statusNote;
  final bool isActive;
  final DateTime createdAt;
  final String? notes;
  final int readinessScore;
  final List<String> alerts;
  final bool isRegistrationExpiringSoon;
  final bool isInsuranceExpiringSoon;
  final List<VehicleServiceRecord> serviceRecords;
  final List<VehicleInspectionRecord> inspectionRecords;

  const Vehicle({
    required this.id,
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
    required this.status,
    this.statusNote,
    required this.isActive,
    required this.createdAt,
    this.notes,
    this.readinessScore = 100,
    this.alerts = const [],
    this.isRegistrationExpiringSoon = false,
    this.isInsuranceExpiringSoon = false,
    this.serviceRecords = const [],
    this.inspectionRecords = const [],
  });

  /// Primary display name — year make model
  String get displayName => '$year $make $model';

  /// Full label including unit code — used for search and quick reference
  String get unitLabel => '[$unitCode] $year $make $model';

  bool get isOutOfService => status.toLowerCase() == 'outofservice';
  bool get isActive_ => status.toLowerCase() == 'active';
  bool get isInMaintenance => status.toLowerCase() == 'inmaintenance';
  bool get isRetired => status.toLowerCase() == 'retired';

  @override
  List<Object?> get props => [
        id,
        unitCode,
        vin,
        make,
        model,
        year,
        color,
        licensePlate,
        province,
        vehicleType,
        passengerCapacity,
        currentOdometerKm,
        acquisitionDate,
        registrationExpiry,
        insuranceProvider,
        insurancePolicyNumber,
        insuranceExpiry,
        status,
        statusNote,
        isActive,
        createdAt,
        notes,
        readinessScore,
        alerts,
        isRegistrationExpiringSoon,
        isInsuranceExpiringSoon,
        serviceRecords,
        inspectionRecords,
      ];
}

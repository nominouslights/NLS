import '../../domain/entities/vehicle.dart';
import 'vehicle_service_record_model.dart';
import 'vehicle_inspection_record_model.dart';

class VehicleModel extends Vehicle {
  const VehicleModel({
    required super.id,
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
    required super.status,
    super.statusNote,
    required super.isActive,
    required super.createdAt,
    super.notes,
    super.readinessScore = 100,
    super.alerts = const [],
    super.isRegistrationExpiringSoon = false,
    super.isInsuranceExpiringSoon = false,
    super.serviceRecords = const [],
    super.inspectionRecords = const [],
  });

  factory VehicleModel.fromJson(Map<String, dynamic> json) {
    final serviceRecordsJson = json['serviceRecords'] as List<dynamic>?;
    final inspectionRecordsJson = json['inspectionRecords'] as List<dynamic>?;
    final alertsJson = json['alerts'] as List<dynamic>?;

    return VehicleModel(
      id: json['id'] as String,
      unitCode: json['unitCode'] as String? ?? '',
      vin: json['vin'] as String? ?? '',
      make: json['make'] as String? ?? '',
      model: json['model'] as String? ?? '',
      year: json['year'] as int? ?? 0,
      color: json['color'] as String? ?? '',
      licensePlate: json['licensePlate'] as String? ?? '',
      province: json['province'] as String? ?? '',
      vehicleType: json['vehicleType'] as String? ?? 'Sedan',
      passengerCapacity: json['passengerCapacity'] as int? ?? 0,
      currentOdometerKm: json['currentOdometerKm'] as int? ?? 0,
      acquisitionDate: _parseDate(json['acquisitionDate']) ?? DateTime(2000),
      registrationExpiry: _parseDate(json['registrationExpiry']),
      insuranceProvider: json['insuranceProvider'] as String?,
      insurancePolicyNumber: json['insurancePolicyNumber'] as String?,
      insuranceExpiry: _parseDate(json['insuranceExpiry']),
      status: json['status'] as String? ?? 'Active',
      statusNote: json['statusNote'] as String?,
      isActive: json['isActive'] as bool? ?? true,
      createdAt: _parseDate(json['createdAt']) ?? DateTime(2000),
      notes: json['notes'] as String?,
      readinessScore: json['readinessScore'] as int? ?? 100,
      alerts: alertsJson != null
          ? alertsJson.map((e) => e as String).toList()
          : const [],
      isRegistrationExpiringSoon:
          json['isRegistrationExpiringSoon'] as bool? ?? false,
      isInsuranceExpiringSoon: json['isInsuranceExpiringSoon'] as bool? ?? false,
      serviceRecords: serviceRecordsJson != null
          ? serviceRecordsJson
              .map((e) => VehicleServiceRecordModel.fromJson(
                  e as Map<String, dynamic>))
              .toList()
          : const [],
      inspectionRecords: inspectionRecordsJson != null
          ? inspectionRecordsJson
              .map((e) => VehicleInspectionRecordModel.fromJson(
                  e as Map<String, dynamic>))
              .toList()
          : const [],
    );
  }

  static DateTime? _parseDate(dynamic value) {
    if (value == null) return null;
    return DateTime.tryParse(value as String);
  }
}

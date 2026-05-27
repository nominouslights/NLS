import '../../domain/entities/vehicle_service_record.dart';

class VehicleServiceRecordModel extends VehicleServiceRecord {
  const VehicleServiceRecordModel({
    required super.id,
    required super.vehicleId,
    required super.serviceCategory,
    super.fluidType,
    required super.title,
    super.description,
    required super.isPlanned,
    required super.serviceStatus,
    required super.priority,
    super.scheduledDate,
    super.startedDate,
    super.completedDate,
    super.odometerAtService,
    super.estimatedCostDollars,
    super.actualCostDollars,
    super.serviceProvider,
    super.technicianName,
    super.partsNotes,
    required super.isWarrantyWork,
    super.nextServiceDueDateUtc,
    super.nextServiceDueOdometerKm,
    required super.createdAt,
  });

  factory VehicleServiceRecordModel.fromJson(Map<String, dynamic> json) {
    return VehicleServiceRecordModel(
      id: json['id'] as String,
      vehicleId: json['vehicleId'] as String? ?? '',
      serviceCategory: json['serviceCategory'] as String? ?? 'Other',
      fluidType: json['fluidType'] as String?,
      title: json['title'] as String? ?? '',
      description: json['description'] as String?,
      isPlanned: json['isPlanned'] as bool? ?? true,
      serviceStatus: json['serviceStatus'] as String? ?? 'Scheduled',
      priority: json['priority'] as String? ?? 'Routine',
      scheduledDate: _parseDate(json['scheduledDate']),
      startedDate: _parseDate(json['startedDate']),
      completedDate: _parseDate(json['completedDate']),
      odometerAtService: json['odometerAtService'] as int?,
      estimatedCostDollars: (json['estimatedCostDollars'] as num?)?.toDouble(),
      actualCostDollars: (json['actualCostDollars'] as num?)?.toDouble(),
      serviceProvider: json['serviceProvider'] as String?,
      technicianName: json['technicianName'] as String?,
      partsNotes: json['partsNotes'] as String?,
      isWarrantyWork: json['isWarrantyWork'] as bool? ?? false,
      nextServiceDueDateUtc: _parseDate(json['nextServiceDueDateUtc']),
      nextServiceDueOdometerKm: json['nextServiceDueOdometerKm'] as int?,
      createdAt: _parseDate(json['createdAt']) ?? DateTime(2000),
    );
  }

  static DateTime? _parseDate(dynamic value) {
    if (value == null) return null;
    return DateTime.tryParse(value as String);
  }
}

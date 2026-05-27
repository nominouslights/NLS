import '../../domain/entities/vehicle_inspection_record.dart';

class VehicleInspectionRecordModel extends VehicleInspectionRecord {
  const VehicleInspectionRecordModel({
    required super.id,
    required super.vehicleId,
    required super.inspectionType,
    required super.inspectedAt,
    super.expiresAt,
    super.inspectorName,
    super.inspectionFacility,
    super.certificateNumber,
    required super.inspectionResult,
    super.deficienciesNotes,
    super.correctiveActionNotes,
    super.costDollars,
    required super.createdAt,
    super.isExpiringSoon = false,
  });

  factory VehicleInspectionRecordModel.fromJson(Map<String, dynamic> json) {
    return VehicleInspectionRecordModel(
      id: json['id'] as String,
      vehicleId: json['vehicleId'] as String? ?? '',
      inspectionType: json['inspectionType'] as String? ?? 'InternalQuality',
      inspectedAt: _parseDate(json['inspectedAt']) ?? DateTime(2000),
      expiresAt: _parseDate(json['expiresAt']),
      inspectorName: json['inspectorName'] as String?,
      inspectionFacility: json['inspectionFacility'] as String?,
      certificateNumber: json['certificateNumber'] as String?,
      inspectionResult: json['inspectionResult'] as String? ?? 'Pass',
      deficienciesNotes: json['deficienciesNotes'] as String?,
      correctiveActionNotes: json['correctiveActionNotes'] as String?,
      costDollars: (json['costDollars'] as num?)?.toDouble(),
      createdAt: _parseDate(json['createdAt']) ?? DateTime(2000),
      isExpiringSoon: json['isExpiringSoon'] as bool? ?? false,
    );
  }

  static DateTime? _parseDate(dynamic value) {
    if (value == null) return null;
    return DateTime.tryParse(value as String);
  }
}

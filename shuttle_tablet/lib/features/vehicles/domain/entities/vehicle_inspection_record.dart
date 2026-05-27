import 'package:equatable/equatable.dart';

class VehicleInspectionRecord extends Equatable {
  final String id;
  final String vehicleId;
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
  final DateTime createdAt;
  final bool isExpiringSoon;

  const VehicleInspectionRecord({
    required this.id,
    required this.vehicleId,
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
    required this.createdAt,
    this.isExpiringSoon = false,
  });

  bool get isPassed => inspectionResult.toLowerCase() == 'pass';
  bool get isFailed => inspectionResult.toLowerCase() == 'fail';

  @override
  List<Object?> get props => [
        id,
        vehicleId,
        inspectionType,
        inspectedAt,
        expiresAt,
        inspectorName,
        inspectionFacility,
        certificateNumber,
        inspectionResult,
        deficienciesNotes,
        correctiveActionNotes,
        costDollars,
        createdAt,
        isExpiringSoon,
      ];
}

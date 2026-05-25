import 'package:equatable/equatable.dart';

enum DocumentType { drugAndAlcoholTest, driversLicense, policeRecordCheck, driverAbstract }

enum TestResult { pass, fail, pending }

enum CheckResult { clear, conditions, flagged }

enum LicenseClass { a, b, c, d, e, f }

class DriverDocument extends Equatable {
  final String id;
  final String driverId;
  final DocumentType documentType;
  final String fileName;
  final String contentType;
  final int fileSizeBytes;
  final DateTime uploadedAt;
  final DateTime? expiryDate;
  final bool isExpiringSoon;

  // Drug & Alcohol Test
  final DateTime? testDate;
  final TestResult? testResultValue;
  final String? testedBy;

  // Driver's License
  final String? licenseNumber;
  final LicenseClass? licenseClass;
  final DateTime? issuedDate;
  final String? licenseProvince;

  // Police Record Check
  final CheckResult? checkResultValue;
  final String? issuingAuthority;

  // Driver Abstract
  final int? violationCount;
  final int? atFaultAccidentCount;

  final String? notes;

  String get documentTypeLabel => switch (documentType) {
        DocumentType.drugAndAlcoholTest => 'Drug & Alcohol Test',
        DocumentType.driversLicense => "Driver's License",
        DocumentType.policeRecordCheck => 'Police Record Check',
        DocumentType.driverAbstract => 'Driver Abstract',
      };

  const DriverDocument({
    required this.id,
    required this.driverId,
    required this.documentType,
    required this.fileName,
    required this.contentType,
    required this.fileSizeBytes,
    required this.uploadedAt,
    this.expiryDate,
    this.isExpiringSoon = false,
    this.testDate,
    this.testResultValue,
    this.testedBy,
    this.licenseNumber,
    this.licenseClass,
    this.issuedDate,
    this.licenseProvince,
    this.checkResultValue,
    this.issuingAuthority,
    this.violationCount,
    this.atFaultAccidentCount,
    this.notes,
  });

  @override
  List<Object?> get props => [
        id,
        driverId,
        documentType,
        fileName,
        contentType,
        fileSizeBytes,
        uploadedAt,
        expiryDate,
        isExpiringSoon,
        testDate,
        testResultValue,
        testedBy,
        licenseNumber,
        licenseClass,
        issuedDate,
        licenseProvince,
        checkResultValue,
        issuingAuthority,
        violationCount,
        atFaultAccidentCount,
        notes,
      ];
}

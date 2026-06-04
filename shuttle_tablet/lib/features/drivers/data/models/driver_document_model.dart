import '../../domain/entities/driver_document.dart';

class DriverDocumentModel extends DriverDocument {
  const DriverDocumentModel({
    required super.id,
    required super.driverId,
    required super.documentType,
    required super.fileName,
    required super.contentType,
    required super.fileSizeBytes,
    required super.uploadedAt,
    super.expiryDate,
    super.isExpiringSoon = false,
    super.testDate,
    super.testResultValue,
    super.testedBy,
    super.licenseNumber,
    super.licenseClass,
    super.issuedDate,
    super.licenseProvince,
    super.checkResultValue,
    super.issuingAuthority,
    super.violationCount,
    super.atFaultAccidentCount,
    super.notes,
  });

  factory DriverDocumentModel.fromJson(Map<String, dynamic> json, String driverId) {
    return DriverDocumentModel(
      id: json['id'] as String,
      driverId: json['driverId'] as String? ?? driverId,
      documentType: _parseDocumentType(json['documentType'] as String? ?? ''),
      fileName: json['fileName'] as String,
      contentType: json['contentType'] as String,
      fileSizeBytes: json['fileSizeBytes'] as int? ?? 0,
      uploadedAt: DateTime.tryParse(json['uploadedAt'] as String? ?? '') ?? DateTime.now(),
      expiryDate: json['expiryDate'] != null
          ? DateTime.tryParse(json['expiryDate'] as String)
          : null,
      isExpiringSoon: json['isExpiringSoon'] as bool? ?? false,
      // Drug & Alcohol
      testDate: json['testDate'] != null
          ? DateTime.tryParse(json['testDate'] as String)
          : null,
      testResultValue: json['testResultValue'] != null
          ? _parseTestResult(json['testResultValue'] as String)
          : null,
      testedBy: json['testedBy'] as String?,
      // Driver's License
      licenseNumber: json['licenseNumber'] as String?,
      licenseClass: json['licenseClass'] != null
          ? _parseLicenseClass(json['licenseClass'] as String)
          : null,
      issuedDate: json['issuedDate'] != null
          ? DateTime.tryParse(json['issuedDate'] as String)
          : null,
      licenseProvince: json['licenseProvince'] as String?,
      // Police Record
      checkResultValue: json['checkResultValue'] != null
          ? _parseCheckResult(json['checkResultValue'] as String)
          : null,
      issuingAuthority: json['issuingAuthority'] as String?,
      // Driver Abstract
      violationCount: json['violationCount'] as int?,
      atFaultAccidentCount: json['atFaultAccidentCount'] as int?,
      notes: json['notes'] as String?,
    );
  }

  static DocumentType _parseDocumentType(String value) {
    return switch (value.toLowerCase()) {
      'drugandalcoholtest' => DocumentType.drugAndAlcoholTest,
      'driverslicense' => DocumentType.driversLicense,
      'policerecordcheck' => DocumentType.policeRecordCheck,
      'driverabstract' => DocumentType.driverAbstract,
      _ => DocumentType.drugAndAlcoholTest,
    };
  }

  static TestResult _parseTestResult(String value) {
    return switch (value.toLowerCase()) {
      'fail' => TestResult.fail,
      'pending' => TestResult.pending,
      _ => TestResult.pass,
    };
  }

  static LicenseClass _parseLicenseClass(String value) {
    return switch (value.toUpperCase()) {
      'B' => LicenseClass.b,
      'C' => LicenseClass.c,
      'D' => LicenseClass.d,
      'E' => LicenseClass.e,
      'F' => LicenseClass.f,
      _ => LicenseClass.a,
    };
  }

  static CheckResult _parseCheckResult(String value) {
    return switch (value.toLowerCase()) {
      'conditions' => CheckResult.conditions,
      'flagged' => CheckResult.flagged,
      _ => CheckResult.clear,
    };
  }
}

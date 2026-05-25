import 'dart:typed_data';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/driver.dart';
import '../entities/driver_document.dart';
import '../entities/driver_roster_entry.dart';

abstract interface class IDriverRepository {
  Future<Either<Failure, List<Driver>>> getDrivers();
  Future<Either<Failure, Driver>> getDriverById(String id);
  Future<Either<Failure, String>> createDriver(CreateDriverParams params);
  Future<Either<Failure, void>> updateDriver(String id, UpdateDriverParams params);
  Future<Either<Failure, void>> deleteDriver(String id);
  Future<Either<Failure, void>> setDriverStatus(String id, DriverStatus status);
  Future<Either<Failure, List<DriverDocument>>> getDriverDocuments(String driverId);
  Future<Either<Failure, String>> uploadDriverDocument(
      String driverId, UploadDocumentParams params);
  Future<Either<Failure, Uint8List>> downloadDriverDocument(
      String driverId, String documentId);
  Future<Either<Failure, void>> deleteDriverDocument(
      String driverId, String documentId);
  Future<Either<Failure, List<DriverRosterEntry>>> getDriverRoster(
      String driverId, DateTime rangeStart, DateTime rangeEnd);
  Future<Either<Failure, List<DriverRosterSummary>>> getFleetRoster(
      DateTime rangeStart, DateTime rangeEnd);
  Future<Either<Failure, String>> upsertRosterEntry(
      String driverId, UpsertRosterEntryParams params);
  Future<Either<Failure, void>> deleteRosterEntry(
      String driverId, String entryId);
}

// ── Params ───────────────────────────────────────────────────────────────────

class CreateDriverParams {
  final String employeeId;
  final String firstName;
  final String lastName;
  final String phone;
  final String email;
  final DateTime hireDate;

  const CreateDriverParams({
    required this.employeeId,
    required this.firstName,
    required this.lastName,
    required this.phone,
    required this.email,
    required this.hireDate,
  });
}

class UpdateDriverParams extends CreateDriverParams {
  final bool isActive;

  const UpdateDriverParams({
    required super.employeeId,
    required super.firstName,
    required super.lastName,
    required super.phone,
    required super.email,
    required super.hireDate,
    required this.isActive,
  });
}

class UploadDocumentParams {
  final DocumentType documentType;
  final String fileName;
  final String contentType;
  final Uint8List fileBytes;
  final DateTime? expiryDate;
  // Drug & Alcohol
  final DateTime? testDate;
  final TestResult? testResultValue;
  final String? testedBy;
  // Driver's License
  final String? licenseNumber;
  final LicenseClass? licenseClass;
  final DateTime? issuedDate;
  final String? licenseProvince;
  // Police Record
  final CheckResult? checkResultValue;
  final String? issuingAuthority;
  // Driver Abstract
  final int? violationCount;
  final int? atFaultAccidentCount;
  final String? notes;

  const UploadDocumentParams({
    required this.documentType,
    required this.fileName,
    required this.contentType,
    required this.fileBytes,
    this.expiryDate,
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
}

class UpsertRosterEntryParams {
  final DateTime entryDate;
  final RosterStatus status;
  final String? shiftStart;
  final String? shiftEnd;

  const UpsertRosterEntryParams({
    required this.entryDate,
    required this.status,
    this.shiftStart,
    this.shiftEnd,
  });
}

class DriverRosterSummary {
  final String driverId;
  final String employeeId;
  final String fullName;
  final List<DriverRosterEntry> entries;

  const DriverRosterSummary({
    required this.driverId,
    required this.employeeId,
    required this.fullName,
    required this.entries,
  });
}

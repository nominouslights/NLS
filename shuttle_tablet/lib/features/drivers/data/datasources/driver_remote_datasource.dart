import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../../domain/entities/driver.dart';
import '../../domain/entities/driver_document.dart';
import '../../domain/entities/driver_roster_entry.dart';
import '../../domain/repositories/i_driver_repository.dart';
import '../models/driver_document_model.dart';
import '../models/driver_model.dart';
import '../models/driver_roster_entry_model.dart';

abstract interface class IDriverRemoteDataSource {
  Future<List<DriverModel>> getDrivers();
  Future<DriverModel> getDriverById(String id);
  Future<String> createDriver(CreateDriverParams params);
  Future<void> updateDriver(String id, UpdateDriverParams params);
  Future<void> deleteDriver(String id);
  Future<void> setDriverStatus(String id, DriverStatus status);
  Future<List<DriverDocumentModel>> getDriverDocuments(String driverId);
  Future<String> uploadDriverDocument(String driverId, UploadDocumentParams params);
  Future<Uint8List> downloadDriverDocument(String driverId, String documentId);
  Future<void> deleteDriverDocument(String driverId, String documentId);
  Future<List<DriverRosterEntryModel>> getDriverRoster(
      String driverId, DateTime rangeStart, DateTime rangeEnd);
  Future<List<DriverRosterSummaryModel>> getFleetRoster(
      DateTime rangeStart, DateTime rangeEnd);
  Future<String> upsertRosterEntry(String driverId, UpsertRosterEntryParams params);
  Future<void> deleteRosterEntry(String driverId, String entryId);
}

class DriverRemoteDataSource implements IDriverRemoteDataSource {
  final Dio _dio;
  const DriverRemoteDataSource(this._dio);

  // ── Drivers ────────────────────────────────────────────────────────────────

  @override
  Future<List<DriverModel>> getDrivers() async {
    try {
      final response = await _dio.get(ApiEndpoints.drivers);
      final list = response.data as List<dynamic>;
      return list
          .map((e) => DriverModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to load drivers');
    }
  }

  @override
  Future<DriverModel> getDriverById(String id) async {
    try {
      final response = await _dio.get(ApiEndpoints.driverById(id));
      return DriverModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to load driver');
    }
  }

  @override
  Future<String> createDriver(CreateDriverParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.drivers,
        data: _createDriverParamsToJson(params),
      );
      final data = response.data as Map<String, dynamic>;
      return data['id'] as String;
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to create driver');
    }
  }

  @override
  Future<void> updateDriver(String id, UpdateDriverParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.driverById(id),
        data: {
          ..._createDriverParamsToJson(params),
          'isActive': params.isActive,
        },
      );
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to update driver');
    }
  }

  @override
  Future<void> deleteDriver(String id) async {
    try {
      await _dio.delete(ApiEndpoints.driverById(id));
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to delete driver');
    }
  }

  @override
  Future<void> setDriverStatus(String id, DriverStatus status) async {
    try {
      await _dio.patch(
        ApiEndpoints.driverStatus(id),
        data: {'status': _driverStatusToString(status)},
      );
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to update driver status');
    }
  }

  // ── Documents ──────────────────────────────────────────────────────────────

  @override
  Future<List<DriverDocumentModel>> getDriverDocuments(String driverId) async {
    try {
      final response = await _dio.get(ApiEndpoints.driverDocuments(driverId));
      final list = response.data as List<dynamic>;
      return list
          .map((e) => DriverDocumentModel.fromJson(e as Map<String, dynamic>, driverId))
          .toList();
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to load documents');
    }
  }

  @override
  Future<String> uploadDriverDocument(
      String driverId, UploadDocumentParams params) async {
    try {
      final formData = FormData.fromMap({
        'file': MultipartFile.fromBytes(
          params.fileBytes,
          filename: params.fileName,
          contentType: DioMediaType.parse(params.contentType),
        ),
        'documentType': _documentTypeToString(params.documentType),
        if (params.expiryDate != null)
          'expiryDate': _formatDate(params.expiryDate!),
        // Drug & Alcohol
        if (params.testDate != null) 'testDate': _formatDate(params.testDate!),
        if (params.testResultValue != null)
          'testResultValue': _capitalize(params.testResultValue!.name),
        if (params.testedBy != null) 'testedBy': params.testedBy,
        // Driver's License
        if (params.licenseNumber != null) 'licenseNumber': params.licenseNumber,
        if (params.licenseClass != null)
          'licenseClass': params.licenseClass!.name.toUpperCase(),
        if (params.issuedDate != null)
          'issuedDate': _formatDate(params.issuedDate!),
        if (params.licenseProvince != null)
          'licenseProvince': params.licenseProvince,
        // Police Record
        if (params.checkResultValue != null)
          'checkResultValue': _capitalize(params.checkResultValue!.name),
        if (params.issuingAuthority != null)
          'issuingAuthority': params.issuingAuthority,
        // Driver Abstract
        if (params.violationCount != null)
          'violationCount': params.violationCount.toString(),
        if (params.atFaultAccidentCount != null)
          'atFaultAccidentCount': params.atFaultAccidentCount.toString(),
        if (params.notes != null) 'notes': params.notes,
      });

      final response = await _dio.post(
        ApiEndpoints.driverDocuments(driverId),
        data: formData,
        options: Options(
          headers: {'Content-Type': 'multipart/form-data'},
        ),
      );
      final data = response.data as Map<String, dynamic>;
      return data['documentId'] as String;
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to upload document');
    }
  }

  @override
  Future<Uint8List> downloadDriverDocument(
      String driverId, String documentId) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.driverDocumentDownload(driverId, documentId),
        options: Options(responseType: ResponseType.bytes),
      );
      return Uint8List.fromList(response.data as List<int>);
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to download document');
    }
  }

  @override
  Future<void> deleteDriverDocument(
      String driverId, String documentId) async {
    try {
      await _dio.delete(
          ApiEndpoints.driverDocumentById(driverId, documentId));
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to delete document');
    }
  }

  // ── Roster ─────────────────────────────────────────────────────────────────

  @override
  Future<List<DriverRosterEntryModel>> getDriverRoster(
      String driverId, DateTime rangeStart, DateTime rangeEnd) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.driverRoster(driverId),
        queryParameters: {
          'rangeStart': _formatDate(rangeStart),
          'rangeEnd': _formatDate(rangeEnd),
        },
      );
      final list = response.data as List<dynamic>;
      return list
          .map((e) =>
              DriverRosterEntryModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to load roster');
    }
  }

  @override
  Future<List<DriverRosterSummaryModel>> getFleetRoster(
      DateTime rangeStart, DateTime rangeEnd) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.fleetRoster,
        queryParameters: {
          'rangeStart': _formatDate(rangeStart),
          'rangeEnd': _formatDate(rangeEnd),
        },
      );
      final list = response.data as List<dynamic>;
      return list
          .map((e) =>
              DriverRosterSummaryModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to load fleet roster');
    }
  }

  @override
  Future<String> upsertRosterEntry(
      String driverId, UpsertRosterEntryParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.driverRoster(driverId),
        data: {
          'entryDate': _formatDate(params.entryDate),
          'status': _rosterStatusToString(params.status),
          if (params.shiftStart != null) 'shiftStart': params.shiftStart,
          if (params.shiftEnd != null) 'shiftEnd': params.shiftEnd,
        },
      );
      final data = response.data as Map<String, dynamic>;
      return data['entryId'] as String;
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to upsert roster entry');
    }
  }

  @override
  Future<void> deleteRosterEntry(String driverId, String entryId) async {
    try {
      await _dio.delete(ApiEndpoints.driverRosterEntry(driverId, entryId));
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to delete roster entry');
    }
  }

  // ── Helpers ────────────────────────────────────────────────────────────────

  Map<String, dynamic> _createDriverParamsToJson(CreateDriverParams p) => {
        'employeeId': p.employeeId,
        'firstName': p.firstName,
        'lastName': p.lastName,
        'phone': p.phone,
        'email': p.email,
        'hireDate': p.hireDate.toUtc().toIso8601String(),
      };

  String _driverStatusToString(DriverStatus s) => _capitalize(s.name);

  String _rosterStatusToString(RosterStatus s) => _capitalize(s.name);

  String _documentTypeToString(DocumentType t) => _capitalize(t.name);

  /// Capitalises the first character, leaving the rest unchanged.
  /// "available" → "Available", "onTrip" → "OnTrip", "drugAndAlcoholTest" → "DrugAndAlcoholTest"
  String _capitalize(String value) =>
      value.isEmpty ? value : value[0].toUpperCase() + value.substring(1);

  /// Formats a DateTime to an ISO-8601 date string (yyyy-MM-dd).
  String _formatDate(DateTime dt) =>
      '${dt.year.toString().padLeft(4, '0')}-'
      '${dt.month.toString().padLeft(2, '0')}-'
      '${dt.day.toString().padLeft(2, '0')}';

  /// Central Dio error handler — always throws, never returns.
  Never _handleDioException(DioException e, String fallbackMessage) {
    if (e.response?.statusCode == 401) throw const UnauthorizedException();
    if (e.response?.statusCode == 404) throw const NotFoundException();
    if (e.response?.statusCode == 409) {
      final msg = (e.response?.data as Map<String, dynamic>?)?['message']
              as String? ??
          'A conflict occurred.';
      throw ConflictException(msg);
    }
    throw ServerException(
      message: e.message ?? fallbackMessage,
      statusCode: e.response?.statusCode,
    );
  }
}

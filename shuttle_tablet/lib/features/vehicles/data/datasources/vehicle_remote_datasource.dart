import 'dart:typed_data';
import 'package:dio/dio.dart';

import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../../domain/repositories/i_vehicle_repository.dart';
import '../models/vehicle_fuel_entry_model.dart';
import '../models/vehicle_model.dart';
import '../models/vehicle_odometer_entry_model.dart';

abstract interface class IVehicleRemoteDataSource {
  Future<List<VehicleModel>> getVehicles();
  Future<VehicleModel> getVehicleById(String id);
  Future<String> createVehicle(CreateVehicleParams params);
  Future<void> updateVehicle(String id, UpdateVehicleParams params);
  Future<void> deleteVehicle(String id);
  Future<void> setVehicleStatus(String id, String status, String? statusNote);
  Future<void> setVehicleOutOfService(String id, String reason);
  Future<void> updateOdometer(String id, int newOdometerKm);
  Future<String> addServiceRecord(String vehicleId, AddServiceRecordParams params);
  Future<void> updateServiceRecord(String vehicleId, String recordId, AddServiceRecordParams params);
  Future<void> completeServiceRecord(String vehicleId, String recordId, CompleteServiceRecordParams params);
  Future<void> deleteServiceRecord(String vehicleId, String recordId);
  Future<String> addInspectionRecord(String vehicleId, AddInspectionRecordParams params);
  Future<void> updateInspectionRecord(String vehicleId, String recordId, AddInspectionRecordParams params);
  Future<void> deleteInspectionRecord(String vehicleId, String recordId);
  Future<List<VehicleFuelEntryModel>> getFuelEntries(String vehicleId);
  Future<String> addFuelEntry(String vehicleId, AddFuelEntryParams params);
  Future<void> deleteFuelEntry(String vehicleId, String entryId);
  Future<Uint8List> getFuelReceipt(String vehicleId, String entryId);
  Future<List<VehicleOdometerEntryModel>> getOdometerHistory(String vehicleId);
}

class VehicleRemoteDataSource implements IVehicleRemoteDataSource {
  final Dio _dio;
  const VehicleRemoteDataSource(this._dio);

  // ── Vehicles ───────────────────────────────────────────────────────────────

  @override
  Future<List<VehicleModel>> getVehicles() async {
    try {
      final response = await _dio.get(ApiEndpoints.vehicles);
      final list = response.data as List<dynamic>;
      return list
          .map((e) => VehicleModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to load vehicles');
    }
  }

  @override
  Future<VehicleModel> getVehicleById(String id) async {
    try {
      final response = await _dio.get(ApiEndpoints.vehicleById(id));
      return VehicleModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to load vehicle');
    }
  }

  @override
  Future<String> createVehicle(CreateVehicleParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.vehicles,
        data: _vehicleParamsToJson(params),
      );
      final data = response.data as Map<String, dynamic>;
      return data['id'] as String;
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to create vehicle');
    }
  }

  @override
  Future<void> updateVehicle(String id, UpdateVehicleParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.vehicleById(id),
        data: {
          ..._vehicleParamsToJson(params),
          'isActive': params.isActive,
        },
      );
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to update vehicle');
    }
  }

  @override
  Future<void> deleteVehicle(String id) async {
    try {
      await _dio.delete(ApiEndpoints.vehicleById(id));
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to delete vehicle');
    }
  }

  @override
  Future<void> setVehicleStatus(String id, String status, String? statusNote) async {
    try {
      await _dio.patch(
        ApiEndpoints.vehicleStatus(id),
        data: {
          'status': status,
          if (statusNote != null) 'statusNote': statusNote,
        },
      );
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to update vehicle status');
    }
  }

  @override
  Future<void> setVehicleOutOfService(String id, String reason) async {
    try {
      await _dio.post(
        ApiEndpoints.vehicleOutOfService(id),
        data: {'reason': reason},
      );
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to mark vehicle out of service');
    }
  }

  @override
  Future<void> updateOdometer(String id, int newOdometerKm) async {
    try {
      await _dio.patch(
        ApiEndpoints.vehicleOdometer(id),
        data: {'newOdometerKm': newOdometerKm},
      );
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to update odometer');
    }
  }

  // ── Service Records ────────────────────────────────────────────────────────

  @override
  Future<String> addServiceRecord(String vehicleId, AddServiceRecordParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.vehicleServiceRecords(vehicleId),
        data: _serviceRecordParamsToJson(params),
      );
      final data = response.data as Map<String, dynamic>;
      return data['recordId'] as String;
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to add service record');
    }
  }

  @override
  Future<void> updateServiceRecord(String vehicleId, String recordId, AddServiceRecordParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.vehicleServiceRecordById(vehicleId, recordId),
        data: _serviceRecordParamsToJson(params),
      );
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to update service record');
    }
  }

  @override
  Future<void> completeServiceRecord(String vehicleId, String recordId, CompleteServiceRecordParams params) async {
    try {
      await _dio.post(
        ApiEndpoints.vehicleServiceRecordComplete(vehicleId, recordId),
        data: {
          'completedDate': params.completedDate.toUtc().toIso8601String(),
          if (params.actualCostDollars != null)
            'actualCostDollars': params.actualCostDollars,
          if (params.odometerAtService != null)
            'odometerAtService': params.odometerAtService,
        },
      );
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to complete service record');
    }
  }

  @override
  Future<void> deleteServiceRecord(String vehicleId, String recordId) async {
    try {
      await _dio.delete(ApiEndpoints.vehicleServiceRecordById(vehicleId, recordId));
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to delete service record');
    }
  }

  // ── Inspection Records ─────────────────────────────────────────────────────

  @override
  Future<String> addInspectionRecord(String vehicleId, AddInspectionRecordParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.vehicleInspectionRecords(vehicleId),
        data: _inspectionRecordParamsToJson(params),
      );
      final data = response.data as Map<String, dynamic>;
      return data['recordId'] as String;
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to add inspection record');
    }
  }

  @override
  Future<void> updateInspectionRecord(String vehicleId, String recordId, AddInspectionRecordParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.vehicleInspectionRecordById(vehicleId, recordId),
        data: _inspectionRecordParamsToJson(params),
      );
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to update inspection record');
    }
  }

  @override
  Future<void> deleteInspectionRecord(String vehicleId, String recordId) async {
    try {
      await _dio.delete(ApiEndpoints.vehicleInspectionRecordById(vehicleId, recordId));
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to delete inspection record');
    }
  }

  // ── Fuel Entries ──────────────────────────────────────────────────────────

  @override
  Future<List<VehicleFuelEntryModel>> getFuelEntries(String vehicleId) async {
    try {
      final response =
          await _dio.get(ApiEndpoints.vehicleFuelEntries(vehicleId));
      final list = response.data as List<dynamic>;
      return list
          .map((e) => VehicleFuelEntryModel.fromJson(
              e as Map<String, dynamic>, vehicleId))
          .toList();
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to load fuel entries');
    }
  }

  @override
  Future<String> addFuelEntry(
      String vehicleId, AddFuelEntryParams params) async {
    try {
      final fields = <String, dynamic>{
        'fuelledAt': params.fuelledAt.toUtc().toIso8601String(),
        'fuelLitres': params.fuelLitres.toString(),
        'totalCostDollars': params.totalCostDollars.toString(),
        if (params.odometerAtFuelling != null)
          'odometerAtFuelling': params.odometerAtFuelling.toString(),
        if (params.notes != null) 'notes': params.notes,
      };

      if (params.receiptPhotoBytes != null) {
        fields['receiptPhoto'] = MultipartFile.fromBytes(
          params.receiptPhotoBytes!,
          filename: params.receiptPhotoFileName ?? 'receipt.jpg',
          contentType:
              DioMediaType.parse(params.receiptPhotoContentType ?? 'image/jpeg'),
        );
        final formData = FormData.fromMap(fields);
        final response = await _dio.post(
          ApiEndpoints.vehicleFuelEntries(vehicleId),
          data: formData,
          options: Options(headers: {'Content-Type': 'multipart/form-data'}),
        );
        return (response.data as Map<String, dynamic>)['entryId'] as String;
      } else {
        final formData = FormData.fromMap(fields);
        final response = await _dio.post(
          ApiEndpoints.vehicleFuelEntries(vehicleId),
          data: formData,
          options: Options(headers: {'Content-Type': 'multipart/form-data'}),
        );
        return (response.data as Map<String, dynamic>)['entryId'] as String;
      }
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to add fuel entry');
    }
  }

  @override
  Future<void> deleteFuelEntry(String vehicleId, String entryId) async {
    try {
      await _dio
          .delete(ApiEndpoints.vehicleFuelEntryById(vehicleId, entryId));
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to delete fuel entry');
    }
  }

  @override
  Future<Uint8List> getFuelReceipt(String vehicleId, String entryId) async {
    try {
      final response = await _dio.get(
        '${ApiEndpoints.vehicleFuelEntryById(vehicleId, entryId)}/receipt',
        options: Options(responseType: ResponseType.bytes),
      );
      return Uint8List.fromList(response.data as List<int>);
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to download fuel receipt');
    }
  }

  @override
  Future<List<VehicleOdometerEntryModel>> getOdometerHistory(
      String vehicleId) async {
    try {
      final response =
          await _dio.get(ApiEndpoints.vehicleOdometerHistory(vehicleId));
      final list = response.data as List<dynamic>;
      return list
          .map((e) =>
              VehicleOdometerEntryModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      _handleDioException(e, 'Failed to load odometer history');
    }
  }

  // ── Helpers ────────────────────────────────────────────────────────────────

  Map<String, dynamic> _vehicleParamsToJson(CreateVehicleParams p) => {
        'unitCode': p.unitCode,
        'vin': p.vin,
        'make': p.make,
        'model': p.model,
        'year': p.year,
        'color': p.color,
        'licensePlate': p.licensePlate,
        'province': p.province,
        'vehicleType': p.vehicleType,
        'passengerCapacity': p.passengerCapacity,
        'currentOdometerKm': p.currentOdometerKm,
        'acquisitionDate': p.acquisitionDate.toUtc().toIso8601String(),
        if (p.registrationExpiry != null)
          'registrationExpiry': p.registrationExpiry!.toUtc().toIso8601String(),
        if (p.insuranceProvider != null) 'insuranceProvider': p.insuranceProvider,
        if (p.insurancePolicyNumber != null)
          'insurancePolicyNumber': p.insurancePolicyNumber,
        if (p.insuranceExpiry != null)
          'insuranceExpiry': p.insuranceExpiry!.toUtc().toIso8601String(),
        if (p.notes != null) 'notes': p.notes,
      };

  Map<String, dynamic> _serviceRecordParamsToJson(AddServiceRecordParams p) => {
        'serviceCategory': p.serviceCategory,
        if (p.fluidType != null) 'fluidType': p.fluidType,
        'title': p.title,
        if (p.description != null) 'description': p.description,
        'isPlanned': p.isPlanned,
        'serviceStatus': p.serviceStatus,
        'priority': p.priority,
        if (p.scheduledDate != null)
          'scheduledDate': p.scheduledDate!.toUtc().toIso8601String(),
        if (p.odometerAtService != null) 'odometerAtService': p.odometerAtService,
        if (p.estimatedCostDollars != null)
          'estimatedCostDollars': p.estimatedCostDollars,
        if (p.serviceProvider != null) 'serviceProvider': p.serviceProvider,
        if (p.technicianName != null) 'technicianName': p.technicianName,
        if (p.partsNotes != null) 'partsNotes': p.partsNotes,
        'isWarrantyWork': p.isWarrantyWork,
        if (p.nextServiceDueDateUtc != null)
          'nextServiceDueDateUtc': p.nextServiceDueDateUtc!.toUtc().toIso8601String(),
        if (p.nextServiceDueOdometerKm != null)
          'nextServiceDueOdometerKm': p.nextServiceDueOdometerKm,
      };

  Map<String, dynamic> _inspectionRecordParamsToJson(AddInspectionRecordParams p) => {
        'inspectionType': p.inspectionType,
        'inspectedAt': p.inspectedAt.toUtc().toIso8601String(),
        if (p.expiresAt != null)
          'expiresAt': p.expiresAt!.toUtc().toIso8601String(),
        if (p.inspectorName != null) 'inspectorName': p.inspectorName,
        if (p.inspectionFacility != null) 'inspectionFacility': p.inspectionFacility,
        if (p.certificateNumber != null) 'certificateNumber': p.certificateNumber,
        'inspectionResult': p.inspectionResult,
        if (p.deficienciesNotes != null) 'deficienciesNotes': p.deficienciesNotes,
        if (p.correctiveActionNotes != null)
          'correctiveActionNotes': p.correctiveActionNotes,
        if (p.costDollars != null) 'costDollars': p.costDollars,
      };

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

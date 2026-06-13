import 'dart:typed_data';
import 'package:fpdart/fpdart.dart';

import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/vehicle.dart';
import '../../domain/entities/vehicle_fuel_entry.dart';
import '../../domain/entities/vehicle_odometer_entry.dart';
import '../../domain/repositories/i_vehicle_repository.dart';
import '../datasources/vehicle_remote_datasource.dart';

class VehicleRepositoryImpl implements IVehicleRepository {
  final IVehicleRemoteDataSource _remote;
  const VehicleRepositoryImpl(this._remote);

  // ── Vehicles ───────────────────────────────────────────────────────────────

  @override
  Future<Either<Failure, List<Vehicle>>> getVehicles() async {
    try {
      final result = await _remote.getVehicles();
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Vehicle>> getVehicleById(String id) async {
    try {
      final result = await _remote.getVehicleById(id);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, String>> createVehicle(CreateVehicleParams params) async {
    try {
      final id = await _remote.createVehicle(params);
      return Right(id);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ConflictException catch (e) {
      return Left(ConflictFailure(e.message));
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> updateVehicle(String id, UpdateVehicleParams params) async {
    try {
      await _remote.updateVehicle(id, params);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ConflictException catch (e) {
      return Left(ConflictFailure(e.message));
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> deleteVehicle(String id) async {
    try {
      await _remote.deleteVehicle(id);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> setVehicleStatus(String id, String status, String? statusNote) async {
    try {
      await _remote.setVehicleStatus(id, status, statusNote);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> setVehicleOutOfService(String id, String reason) async {
    try {
      await _remote.setVehicleOutOfService(id, reason);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> updateOdometer(String id, int newOdometerKm) async {
    try {
      await _remote.updateOdometer(id, newOdometerKm);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  // ── Service Records ────────────────────────────────────────────────────────

  @override
  Future<Either<Failure, String>> addServiceRecord(String vehicleId, AddServiceRecordParams params) async {
    try {
      final id = await _remote.addServiceRecord(vehicleId, params);
      return Right(id);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> updateServiceRecord(String vehicleId, String recordId, AddServiceRecordParams params) async {
    try {
      await _remote.updateServiceRecord(vehicleId, recordId, params);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> completeServiceRecord(String vehicleId, String recordId, CompleteServiceRecordParams params) async {
    try {
      await _remote.completeServiceRecord(vehicleId, recordId, params);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> deleteServiceRecord(String vehicleId, String recordId) async {
    try {
      await _remote.deleteServiceRecord(vehicleId, recordId);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  // ── Inspection Records ─────────────────────────────────────────────────────

  @override
  Future<Either<Failure, String>> addInspectionRecord(String vehicleId, AddInspectionRecordParams params) async {
    try {
      final id = await _remote.addInspectionRecord(vehicleId, params);
      return Right(id);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> updateInspectionRecord(String vehicleId, String recordId, AddInspectionRecordParams params) async {
    try {
      await _remote.updateInspectionRecord(vehicleId, recordId, params);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> deleteInspectionRecord(String vehicleId, String recordId) async {
    try {
      await _remote.deleteInspectionRecord(vehicleId, recordId);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, List<VehicleFuelEntry>>> getFuelEntries(String vehicleId) async {
    try {
      final models = await _remote.getFuelEntries(vehicleId);
      return Right(models);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, String>> addFuelEntry(String vehicleId, AddFuelEntryParams params) async {
    try {
      final id = await _remote.addFuelEntry(vehicleId, params);
      return Right(id);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> deleteFuelEntry(String vehicleId, String entryId) async {
    try {
      await _remote.deleteFuelEntry(vehicleId, entryId);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Uint8List>> getFuelReceipt(String vehicleId, String entryId) async {
    try {
      final bytes = await _remote.getFuelReceipt(vehicleId, entryId);
      return Right(bytes);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, List<VehicleOdometerEntry>>> getOdometerHistory(String vehicleId) async {
    try {
      final models = await _remote.getOdometerHistory(vehicleId);
      return Right(models);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }
}

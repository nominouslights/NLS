import 'dart:typed_data';

import 'package:fpdart/fpdart.dart';

import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/driver.dart';
import '../../domain/entities/driver_document.dart';
import '../../domain/entities/driver_roster_entry.dart';
import '../../domain/repositories/i_driver_repository.dart';
import '../datasources/driver_remote_datasource.dart';

class DriverRepositoryImpl implements IDriverRepository {
  final IDriverRemoteDataSource _remote;
  const DriverRepositoryImpl(this._remote);

  // ── Drivers ────────────────────────────────────────────────────────────────

  @override
  Future<Either<Failure, List<Driver>>> getDrivers() async {
    try {
      final result = await _remote.getDrivers();
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Driver>> getDriverById(String id) async {
    try {
      final result = await _remote.getDriverById(id);
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
  Future<Either<Failure, String>> createDriver(CreateDriverParams params) async {
    try {
      final id = await _remote.createDriver(params);
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
  Future<Either<Failure, void>> updateDriver(
      String id, UpdateDriverParams params) async {
    try {
      await _remote.updateDriver(id, params);
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
  Future<Either<Failure, void>> deleteDriver(String id) async {
    try {
      await _remote.deleteDriver(id);
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
  Future<Either<Failure, void>> setDriverStatus(
      String id, DriverStatus status) async {
    try {
      await _remote.setDriverStatus(id, status);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  // ── Documents ──────────────────────────────────────────────────────────────

  @override
  Future<Either<Failure, List<DriverDocument>>> getDriverDocuments(
      String driverId) async {
    try {
      final result = await _remote.getDriverDocuments(driverId);
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
  Future<Either<Failure, String>> uploadDriverDocument(
      String driverId, UploadDocumentParams params) async {
    try {
      final id = await _remote.uploadDriverDocument(driverId, params);
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
  Future<Either<Failure, Uint8List>> downloadDriverDocument(
      String driverId, String documentId) async {
    try {
      final bytes = await _remote.downloadDriverDocument(driverId, documentId);
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
  Future<Either<Failure, void>> deleteDriverDocument(
      String driverId, String documentId) async {
    try {
      await _remote.deleteDriverDocument(driverId, documentId);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  // ── Roster ─────────────────────────────────────────────────────────────────

  @override
  Future<Either<Failure, List<DriverRosterEntry>>> getDriverRoster(
      String driverId, DateTime rangeStart, DateTime rangeEnd) async {
    try {
      final result =
          await _remote.getDriverRoster(driverId, rangeStart, rangeEnd);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, List<DriverRosterSummary>>> getFleetRoster(
      DateTime rangeStart, DateTime rangeEnd) async {
    try {
      final models = await _remote.getFleetRoster(rangeStart, rangeEnd);
      // Map DriverRosterSummaryModel → DriverRosterSummary
      final summaries = models
          .map((m) => DriverRosterSummary(
                driverId: m.driverId,
                employeeId: m.employeeId,
                fullName: m.fullName,
                entries: m.entries,
              ))
          .toList();
      return Right(summaries);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, String>> upsertRosterEntry(
      String driverId, UpsertRosterEntryParams params) async {
    try {
      final id = await _remote.upsertRosterEntry(driverId, params);
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
  Future<Either<Failure, void>> deleteRosterEntry(
      String driverId, String entryId) async {
    try {
      await _remote.deleteRosterEntry(driverId, entryId);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }
}

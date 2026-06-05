import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/trip.dart';
import '../../domain/entities/trip_passenger.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../datasources/trip_remote_datasource.dart';

class TripRepositoryImpl implements ITripRepository {
  final ITripRemoteDataSource _remoteDataSource;
  const TripRepositoryImpl(this._remoteDataSource);

  @override
  Future<Either<Failure, List<Trip>>> getTrips({
    TripStatus? status,
    String? clientId,
    String? driverId,
    String? vehicleId,
    TripServiceType? serviceType,
  }) async {
    try {
      final result = await _remoteDataSource.getTrips(
        status: status,
        clientId: clientId,
        driverId: driverId,
        vehicleId: vehicleId,
        serviceType: serviceType,
      );
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Trip>> getTripById(String id) async {
    try {
      final result = await _remoteDataSource.getTripById(id);
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
  Future<Either<Failure, String>> createTrip(CreateTripParams params) async {
    try {
      final id = await _remoteDataSource.createTrip(params);
      return Right(id);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> updateTrip(
      String id, UpdateTripParams params) async {
    try {
      await _remoteDataSource.updateTrip(id, params);
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
  Future<Either<Failure, void>> deleteTrip(String id) async {
    try {
      await _remoteDataSource.deleteTrip(id);
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
  Future<Either<Failure, void>> assignDriver(
      String tripId, AssignDriverParams params) async {
    try {
      await _remoteDataSource.assignDriver(tripId, params);
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
  Future<Either<Failure, void>> dispatchTrip(String tripId) async {
    try {
      await _remoteDataSource.dispatchTrip(tripId);
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
  Future<Either<Failure, void>> updateTripStatus(
      String tripId, TripStatus status) async {
    try {
      await _remoteDataSource.updateTripStatus(tripId, status);
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
  Future<Either<Failure, void>> submitPreInspection(
      String tripId, SubmitPreInspectionParams params) async {
    try {
      await _remoteDataSource.submitPreInspection(tripId, params);
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
  Future<Either<Failure, void>> submitPostReport(
      String tripId, SubmitPostReportParams params) async {
    try {
      await _remoteDataSource.submitPostReport(tripId, params);
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
  Future<Either<Failure, List<TripPassenger>>> getPassengers(
      String tripId) async {
    try {
      final result = await _remoteDataSource.getPassengers(tripId);
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
  Future<Either<Failure, String>> addPassenger(
      AddPassengerParams params) async {
    try {
      final id = await _remoteDataSource.addPassenger(params);
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
  Future<Either<Failure, void>> removePassenger(
      String tripId, String passengerId) async {
    try {
      await _remoteDataSource.removePassenger(tripId, passengerId);
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
  Future<Either<Failure, void>> updatePassengerPaymentStatus(
      UpdatePassengerPaymentStatusParams params) async {
    try {
      await _remoteDataSource.updatePassengerPaymentStatus(params);
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
  Future<Either<Failure, String>> addCargoItem(
      AddCargoItemParams params) async {
    try {
      final id = await _remoteDataSource.addCargoItem(params);
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
  Future<Either<Failure, void>> removeCargoItem(
      String tripId, String cargoItemId) async {
    try {
      await _remoteDataSource.removeCargoItem(tripId, cargoItemId);
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

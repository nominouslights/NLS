import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/saved_location.dart';
import '../../domain/repositories/i_location_repository.dart';
import '../datasources/location_remote_datasource.dart';

class LocationRepositoryImpl implements ILocationRepository {
  final ILocationRemoteDataSource _remoteDataSource;
  const LocationRepositoryImpl(this._remoteDataSource);

  @override
  Future<Either<Failure, List<SavedLocation>>> getLocations() async {
    try {
      final result = await _remoteDataSource.getLocations();
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, String>> createLocation(CreateLocationParams params) async {
    try {
      final id = await _remoteDataSource.createLocation(params);
      return Right(id);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> updateLocation(String id, UpdateLocationParams params) async {
    try {
      await _remoteDataSource.updateLocation(id, params);
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
  Future<Either<Failure, void>> deleteLocation(String id) async {
    try {
      await _remoteDataSource.deleteLocation(id);
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

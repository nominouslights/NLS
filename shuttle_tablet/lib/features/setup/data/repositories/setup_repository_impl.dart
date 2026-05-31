import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/repositories/i_setup_repository.dart';
import '../datasources/setup_remote_datasource.dart';

class SetupRepositoryImpl implements ISetupRepository {
  final ISetupRemoteDataSource _remoteDataSource;
  const SetupRepositoryImpl(this._remoteDataSource);

  @override
  Future<Either<Failure, bool>> getSetupStatus() async {
    try {
      final result = await _remoteDataSource.getSetupStatus();
      return Right(result);
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> initializeSystem(String email, String password) async {
    try {
      await _remoteDataSource.initializeSystem(email, password);
      return const Right(null);
    } on ConflictException catch (e) {
      return Left(ConflictFailure(e.message));
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }
}

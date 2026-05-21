import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/pending_user.dart';
import '../../domain/repositories/i_users_repository.dart';
import '../datasources/users_remote_datasource.dart';

class UsersRepositoryImpl implements IUsersRepository {
  final IUsersRemoteDataSource _remoteDataSource;
  const UsersRepositoryImpl(this._remoteDataSource);

  @override
  Future<Either<Failure, List<PendingUser>>> getPendingUsers() async {
    try {
      final result = await _remoteDataSource.getPendingUsers();
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> approveUser(String id) async {
    try {
      await _remoteDataSource.approveUser(id);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> rejectUser(String id) async {
    try {
      await _remoteDataSource.rejectUser(id);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }
}

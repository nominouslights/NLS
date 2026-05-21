import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/auth_token.dart';
import '../../domain/repositories/i_auth_repository.dart';
import '../datasources/auth_remote_datasource.dart';

class AuthRepositoryImpl implements IAuthRepository {
  final IAuthRemoteDataSource _remoteDataSource;
  const AuthRepositoryImpl(this._remoteDataSource);

  @override
  Future<Either<Failure, AuthToken>> login(String email, String password) async {
    try {
      final result = await _remoteDataSource.login(email, password);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, AuthToken>> refreshToken(String token) async {
    try {
      final result = await _remoteDataSource.refreshToken(token);
      return Right(result);
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> register(String email, String password, String role) async {
    try {
      await _remoteDataSource.register(email, password, role);
      return const Right(null);
    } on ConflictException catch (e) {
      return Left(ConflictFailure(e.message));
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }
}

import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/auth_token.dart';

abstract interface class IAuthRepository {
  Future<Either<Failure, AuthToken>> login(String email, String password);
  Future<Either<Failure, AuthToken>> refreshToken(String refreshToken);
  Future<Either<Failure, void>> register(String email, String password, String role);
  Future<Either<Failure, void>> changePassword(String currentPassword, String newPassword);
}

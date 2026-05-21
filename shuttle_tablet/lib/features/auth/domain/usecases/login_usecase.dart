import 'package:equatable/equatable.dart';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/auth_token.dart';
import '../repositories/i_auth_repository.dart';

class LoginUseCase implements UseCase<AuthToken, LoginParams> {
  final IAuthRepository _repository;
  const LoginUseCase(this._repository);

  @override
  Future<Either<Failure, AuthToken>> call(LoginParams params) =>
      _repository.login(params.email, params.password);
}

class LoginParams extends Equatable {
  final String email;
  final String password;
  const LoginParams({required this.email, required this.password});

  @override
  List<Object?> get props => [email, password];
}

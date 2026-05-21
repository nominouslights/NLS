import 'package:equatable/equatable.dart';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_auth_repository.dart';

class RegisterUseCase implements UseCase<void, RegisterParams> {
  final IAuthRepository _repository;
  const RegisterUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(RegisterParams params) =>
      _repository.register(params.email, params.password, params.role);
}

class RegisterParams extends Equatable {
  final String email;
  final String password;
  final String role;
  const RegisterParams({required this.email, required this.password, required this.role});

  @override
  List<Object?> get props => [email, password, role];
}

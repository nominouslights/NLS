import 'package:equatable/equatable.dart';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_auth_repository.dart';

class ChangePasswordUseCase implements UseCase<void, ChangePasswordParams> {
  final IAuthRepository _repository;
  const ChangePasswordUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(ChangePasswordParams params) =>
      _repository.changePassword(params.currentPassword, params.newPassword);
}

class ChangePasswordParams extends Equatable {
  final String currentPassword;
  final String newPassword;
  const ChangePasswordParams({
    required this.currentPassword,
    required this.newPassword,
  });

  @override
  List<Object?> get props => [currentPassword, newPassword];
}

import 'package:equatable/equatable.dart';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_users_repository.dart';

class ApproveUserUseCase implements UseCase<void, UserIdParams> {
  final IUsersRepository _repository;
  const ApproveUserUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UserIdParams params) =>
      _repository.approveUser(params.id);
}

class UserIdParams extends Equatable {
  final String id;
  const UserIdParams(this.id);

  @override
  List<Object?> get props => [id];
}

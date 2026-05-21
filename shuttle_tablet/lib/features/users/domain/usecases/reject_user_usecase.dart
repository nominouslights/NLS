import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_users_repository.dart';
import 'approve_user_usecase.dart';

class RejectUserUseCase implements UseCase<void, UserIdParams> {
  final IUsersRepository _repository;
  const RejectUserUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UserIdParams params) =>
      _repository.rejectUser(params.id);
}

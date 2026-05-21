import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/pending_user.dart';
import '../repositories/i_users_repository.dart';

class GetPendingUsersUseCase implements UseCase<List<PendingUser>, NoParams> {
  final IUsersRepository _repository;
  const GetPendingUsersUseCase(this._repository);

  @override
  Future<Either<Failure, List<PendingUser>>> call(NoParams params) =>
      _repository.getPendingUsers();
}

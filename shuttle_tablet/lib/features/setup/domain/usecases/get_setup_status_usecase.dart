import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_setup_repository.dart';

class GetSetupStatusUseCase implements UseCase<bool, NoParams> {
  final ISetupRepository _repository;
  const GetSetupStatusUseCase(this._repository);

  @override
  Future<Either<Failure, bool>> call(NoParams params) =>
      _repository.getSetupStatus();
}

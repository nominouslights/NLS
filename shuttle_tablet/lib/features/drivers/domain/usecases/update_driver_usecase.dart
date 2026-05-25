import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_driver_repository.dart';

class UpdateDriverUseCaseParams {
  final String id;
  final UpdateDriverParams params;
  const UpdateDriverUseCaseParams(this.id, this.params);
}

class UpdateDriverUseCase implements UseCase<void, UpdateDriverUseCaseParams> {
  final IDriverRepository _repository;
  const UpdateDriverUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdateDriverUseCaseParams params) =>
      _repository.updateDriver(params.id, params.params);
}

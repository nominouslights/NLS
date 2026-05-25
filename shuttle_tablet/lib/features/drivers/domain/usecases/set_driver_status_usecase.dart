import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/driver.dart';
import '../repositories/i_driver_repository.dart';

class SetDriverStatusParams {
  final String id;
  final DriverStatus status;
  const SetDriverStatusParams(this.id, this.status);
}

class SetDriverStatusUseCase implements UseCase<void, SetDriverStatusParams> {
  final IDriverRepository _repository;
  const SetDriverStatusUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(SetDriverStatusParams params) =>
      _repository.setDriverStatus(params.id, params.status);
}

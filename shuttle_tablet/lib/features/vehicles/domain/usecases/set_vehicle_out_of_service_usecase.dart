import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class SetOutOfServiceParams {
  final String id;
  final String reason;
  const SetOutOfServiceParams(this.id, this.reason);
}

class SetVehicleOutOfServiceUseCase implements UseCase<void, SetOutOfServiceParams> {
  final IVehicleRepository _repository;
  const SetVehicleOutOfServiceUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(SetOutOfServiceParams params) =>
      _repository.setVehicleOutOfService(params.id, params.reason);
}

import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class SetVehicleStatusParams {
  final String id;
  final String status;
  final String? statusNote;
  const SetVehicleStatusParams(this.id, this.status, {this.statusNote});
}

class SetVehicleStatusUseCase implements UseCase<void, SetVehicleStatusParams> {
  final IVehicleRepository _repository;
  const SetVehicleStatusUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(SetVehicleStatusParams params) =>
      _repository.setVehicleStatus(params.id, params.status, params.statusNote);
}

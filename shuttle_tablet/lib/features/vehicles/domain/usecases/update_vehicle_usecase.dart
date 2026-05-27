import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class UpdateVehicleParams2 {
  final String id;
  final UpdateVehicleParams data;
  const UpdateVehicleParams2(this.id, this.data);
}

class UpdateVehicleUseCase implements UseCase<void, UpdateVehicleParams2> {
  final IVehicleRepository _repository;
  const UpdateVehicleUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdateVehicleParams2 params) =>
      _repository.updateVehicle(params.id, params.data);
}

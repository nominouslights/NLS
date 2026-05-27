import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/vehicle.dart';
import '../repositories/i_vehicle_repository.dart';

class VehicleIdParams {
  final String id;
  const VehicleIdParams(this.id);
}

class GetVehicleByIdUseCase implements UseCase<Vehicle, VehicleIdParams> {
  final IVehicleRepository _repository;
  const GetVehicleByIdUseCase(this._repository);

  @override
  Future<Either<Failure, Vehicle>> call(VehicleIdParams params) =>
      _repository.getVehicleById(params.id);
}

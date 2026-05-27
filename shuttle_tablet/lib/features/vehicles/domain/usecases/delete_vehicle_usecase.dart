import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';
import 'get_vehicle_by_id_usecase.dart';

export 'get_vehicle_by_id_usecase.dart' show VehicleIdParams;

class DeleteVehicleUseCase implements UseCase<void, VehicleIdParams> {
  final IVehicleRepository _repository;
  const DeleteVehicleUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(VehicleIdParams params) =>
      _repository.deleteVehicle(params.id);
}

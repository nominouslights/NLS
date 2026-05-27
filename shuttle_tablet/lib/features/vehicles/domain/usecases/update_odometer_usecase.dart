import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class UpdateOdometerParams {
  final String id;
  final int newOdometerKm;
  const UpdateOdometerParams(this.id, this.newOdometerKm);
}

class UpdateOdometerUseCase implements UseCase<void, UpdateOdometerParams> {
  final IVehicleRepository _repository;
  const UpdateOdometerUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdateOdometerParams params) =>
      _repository.updateOdometer(params.id, params.newOdometerKm);
}

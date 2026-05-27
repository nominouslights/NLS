import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class CreateVehicleUseCase implements UseCase<String, CreateVehicleParams> {
  final IVehicleRepository _repository;
  const CreateVehicleUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(CreateVehicleParams params) =>
      _repository.createVehicle(params);
}

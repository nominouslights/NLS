import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/vehicle.dart';
import '../repositories/i_vehicle_repository.dart';

class GetVehiclesUseCase implements UseCase<List<Vehicle>, NoParams> {
  final IVehicleRepository _repository;
  const GetVehiclesUseCase(this._repository);

  @override
  Future<Either<Failure, List<Vehicle>>> call(NoParams params) =>
      _repository.getVehicles();
}

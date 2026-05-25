import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/driver.dart';
import '../repositories/i_driver_repository.dart';

class DriverIdParams {
  final String id;
  const DriverIdParams(this.id);
}

class GetDriverByIdUseCase implements UseCase<Driver, DriverIdParams> {
  final IDriverRepository _repository;
  const GetDriverByIdUseCase(this._repository);

  @override
  Future<Either<Failure, Driver>> call(DriverIdParams params) =>
      _repository.getDriverById(params.id);
}

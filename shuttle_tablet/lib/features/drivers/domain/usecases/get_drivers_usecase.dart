import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/driver.dart';
import '../repositories/i_driver_repository.dart';

class GetDriversUseCase implements UseCase<List<Driver>, NoParams> {
  final IDriverRepository _repository;
  const GetDriversUseCase(this._repository);

  @override
  Future<Either<Failure, List<Driver>>> call(NoParams params) =>
      _repository.getDrivers();
}

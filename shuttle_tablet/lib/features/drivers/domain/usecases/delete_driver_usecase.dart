import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_driver_repository.dart';
import 'get_driver_by_id_usecase.dart';

class DeleteDriverUseCase implements UseCase<void, DriverIdParams> {
  final IDriverRepository _repository;
  const DeleteDriverUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(DriverIdParams params) =>
      _repository.deleteDriver(params.id);
}

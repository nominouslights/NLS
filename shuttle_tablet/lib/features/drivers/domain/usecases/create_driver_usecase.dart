import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_driver_repository.dart';

class CreateDriverUseCase implements UseCase<String, CreateDriverParams> {
  final IDriverRepository _repository;
  const CreateDriverUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(CreateDriverParams params) =>
      _repository.createDriver(params);
}

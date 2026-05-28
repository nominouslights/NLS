import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_location_repository.dart';

class CreateLocationUseCase implements UseCase<String, CreateLocationParams> {
  final ILocationRepository _repository;
  const CreateLocationUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(CreateLocationParams params) =>
      _repository.createLocation(params);
}

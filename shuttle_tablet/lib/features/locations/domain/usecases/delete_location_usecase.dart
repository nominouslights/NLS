import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_location_repository.dart';

class DeleteLocationUseCase implements UseCase<void, DeleteLocationParams> {
  final ILocationRepository _repository;
  const DeleteLocationUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(DeleteLocationParams params) =>
      _repository.deleteLocation(params.id);
}

class DeleteLocationParams {
  final String id;
  const DeleteLocationParams(this.id);
}

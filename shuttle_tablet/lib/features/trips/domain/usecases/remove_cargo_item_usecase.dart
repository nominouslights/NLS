import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class RemoveCargoItemUseCase implements UseCase<void, RemoveCargoItemParams> {
  final ITripRepository _repository;
  const RemoveCargoItemUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(RemoveCargoItemParams params) =>
      _repository.removeCargoItem(params.tripId, params.cargoItemId);
}

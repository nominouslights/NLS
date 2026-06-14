import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class AddStopUseCase implements UseCase<void, AddStopParams> {
  final ITripRepository _repository;
  const AddStopUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(AddStopParams params) =>
      _repository.addStop(params);
}

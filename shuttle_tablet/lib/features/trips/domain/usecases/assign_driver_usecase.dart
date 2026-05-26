import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class AssignDriverUseCaseParams {
  final String tripId;
  final AssignDriverParams data;
  const AssignDriverUseCaseParams({required this.tripId, required this.data});
}

class AssignDriverUseCase implements UseCase<void, AssignDriverUseCaseParams> {
  final ITripRepository _repository;
  const AssignDriverUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(AssignDriverUseCaseParams params) =>
      _repository.assignDriver(params.tripId, params.data);
}

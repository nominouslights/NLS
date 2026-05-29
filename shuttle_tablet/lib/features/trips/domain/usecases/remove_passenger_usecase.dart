import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class RemovePassengerParams {
  final String tripId;
  final String passengerId;
  const RemovePassengerParams({required this.tripId, required this.passengerId});
}

class RemovePassengerUseCase implements UseCase<void, RemovePassengerParams> {
  final ITripRepository _repository;
  const RemovePassengerUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(RemovePassengerParams params) =>
      _repository.removePassenger(params.tripId, params.passengerId);
}

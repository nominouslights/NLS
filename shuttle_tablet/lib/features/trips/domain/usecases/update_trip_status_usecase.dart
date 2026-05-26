import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/trip.dart';
import '../repositories/i_trip_repository.dart';

class UpdateTripStatusParams {
  final String tripId;
  final TripStatus status;
  const UpdateTripStatusParams({required this.tripId, required this.status});
}

class UpdateTripStatusUseCase
    implements UseCase<void, UpdateTripStatusParams> {
  final ITripRepository _repository;
  const UpdateTripStatusUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdateTripStatusParams params) =>
      _repository.updateTripStatus(params.tripId, params.status);
}

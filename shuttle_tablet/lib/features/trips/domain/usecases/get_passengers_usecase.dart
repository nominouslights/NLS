import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/trip_passenger.dart';
import '../repositories/i_trip_repository.dart';

class GetPassengersUseCase implements UseCase<List<TripPassenger>, TripIdParams> {
  final ITripRepository _repository;
  const GetPassengersUseCase(this._repository);

  @override
  Future<Either<Failure, List<TripPassenger>>> call(TripIdParams params) =>
      _repository.getPassengers(params.tripId);
}

class TripIdParams {
  final String tripId;
  const TripIdParams(this.tripId);
}

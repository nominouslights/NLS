import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/trip.dart';
import '../repositories/i_trip_repository.dart';

class TripIdParams {
  final String id;
  const TripIdParams(this.id);
}

class GetTripByIdUseCase implements UseCase<Trip, TripIdParams> {
  final ITripRepository _repository;
  const GetTripByIdUseCase(this._repository);

  @override
  Future<Either<Failure, Trip>> call(TripIdParams params) =>
      _repository.getTripById(params.id);
}

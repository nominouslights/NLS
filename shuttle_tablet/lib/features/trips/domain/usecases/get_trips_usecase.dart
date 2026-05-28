import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/trip.dart';
import '../repositories/i_trip_repository.dart';

class GetTripsParams {
  final TripStatus? status;
  final String? clientId;
  final String? driverId;
  final String? vehicleId;

  const GetTripsParams({this.status, this.clientId, this.driverId, this.vehicleId});
}

class GetTripsUseCase implements UseCase<List<Trip>, GetTripsParams> {
  final ITripRepository _repository;
  const GetTripsUseCase(this._repository);

  @override
  Future<Either<Failure, List<Trip>>> call(GetTripsParams params) =>
      _repository.getTrips(
        status: params.status,
        clientId: params.clientId,
        driverId: params.driverId,
        vehicleId: params.vehicleId,
      );
}

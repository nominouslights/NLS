import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../repositories/i_trip_repository.dart';

class MarkStopArrivedParams {
  final String tripId;
  final String stopId;
  const MarkStopArrivedParams({required this.tripId, required this.stopId});
}

class MarkStopArrivedUseCase {
  final ITripRepository _repository;
  const MarkStopArrivedUseCase(this._repository);

  Future<Either<Failure, void>> call(MarkStopArrivedParams params) =>
      _repository.markStopArrived(params.tripId, params.stopId);
}

import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../repositories/i_trip_repository.dart';

class MarkStopDepartedParams {
  final String tripId;
  final String stopId;
  const MarkStopDepartedParams({required this.tripId, required this.stopId});
}

class MarkStopDepartedUseCase {
  final ITripRepository _repository;
  const MarkStopDepartedUseCase(this._repository);

  Future<Either<Failure, void>> call(MarkStopDepartedParams params) =>
      _repository.markStopDeparted(params.tripId, params.stopId);
}

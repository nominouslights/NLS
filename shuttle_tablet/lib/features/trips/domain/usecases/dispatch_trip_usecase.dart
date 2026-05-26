import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';
import 'get_trip_by_id_usecase.dart';

class DispatchTripUseCase implements UseCase<void, TripIdParams> {
  final ITripRepository _repository;
  const DispatchTripUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(TripIdParams params) =>
      _repository.dispatchTrip(params.id);
}

import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';
import 'get_trip_by_id_usecase.dart';

class DeleteTripUseCase implements UseCase<void, TripIdParams> {
  final ITripRepository _repository;
  const DeleteTripUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(TripIdParams params) =>
      _repository.deleteTrip(params.id);
}

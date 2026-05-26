import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class UpdateTripUseCaseParams {
  final String id;
  final UpdateTripParams data;
  const UpdateTripUseCaseParams({required this.id, required this.data});
}

class UpdateTripUseCase implements UseCase<void, UpdateTripUseCaseParams> {
  final ITripRepository _repository;
  const UpdateTripUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdateTripUseCaseParams params) =>
      _repository.updateTrip(params.id, params.data);
}

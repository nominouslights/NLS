import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class CreateTripUseCase implements UseCase<String, CreateTripParams> {
  final ITripRepository _repository;
  const CreateTripUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(CreateTripParams params) =>
      _repository.createTrip(params);
}

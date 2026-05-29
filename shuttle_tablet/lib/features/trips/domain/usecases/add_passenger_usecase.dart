import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class AddPassengerUseCase implements UseCase<String, AddPassengerParams> {
  final ITripRepository _repository;
  const AddPassengerUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(AddPassengerParams params) =>
      _repository.addPassenger(params);
}

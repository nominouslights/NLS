import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class UpdatePassengerBoardingStatusUseCase
    implements UseCase<void, UpdatePassengerBoardingStatusParams> {
  final ITripRepository _repository;
  const UpdatePassengerBoardingStatusUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(
          UpdatePassengerBoardingStatusParams params) =>
      _repository.updatePassengerBoardingStatus(params);
}

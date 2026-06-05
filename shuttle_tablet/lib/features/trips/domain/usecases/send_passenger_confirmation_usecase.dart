import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class SendPassengerConfirmationUseCase
    implements UseCase<void, SendPassengerConfirmationParams> {
  final ITripRepository _repository;
  const SendPassengerConfirmationUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(SendPassengerConfirmationParams params) =>
      _repository.sendPassengerConfirmation(params);
}

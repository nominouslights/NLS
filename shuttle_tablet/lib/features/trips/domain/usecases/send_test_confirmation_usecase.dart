import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class SendTestConfirmationParams {
  final String tripId;
  final String passengerId;
  final String direction;
  final String testEmail;

  const SendTestConfirmationParams({
    required this.tripId,
    required this.passengerId,
    required this.direction,
    required this.testEmail,
  });
}

class SendTestConfirmationUseCase
    implements UseCase<void, SendTestConfirmationParams> {
  final ITripRepository _repository;
  const SendTestConfirmationUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(SendTestConfirmationParams params) =>
      _repository.sendTestConfirmation(
        params.tripId,
        params.passengerId,
        params.direction,
        params.testEmail,
      );
}

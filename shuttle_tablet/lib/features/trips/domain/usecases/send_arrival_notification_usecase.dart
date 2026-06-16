import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../repositories/i_trip_repository.dart';

class SendArrivalNotificationUseCase {
  final ITripRepository _repository;
  const SendArrivalNotificationUseCase(this._repository);

  Future<Either<Failure, void>> call(String tripId) =>
      _repository.sendArrivalNotification(tripId);
}

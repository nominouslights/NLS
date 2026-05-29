import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class UpdatePassengerPaymentStatusUseCase
    implements UseCase<void, UpdatePassengerPaymentStatusParams> {
  final ITripRepository _repository;
  const UpdatePassengerPaymentStatusUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdatePassengerPaymentStatusParams params) =>
      _repository.updatePassengerPaymentStatus(params);
}

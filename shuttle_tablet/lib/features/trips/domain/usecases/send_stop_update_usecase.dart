import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class SendStopUpdateUseCase implements UseCase<void, SendStopUpdateParams> {
  final ITripRepository _repository;
  const SendStopUpdateUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(SendStopUpdateParams params) =>
      _repository.sendStopUpdate(params);
}

import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class CompleteServiceRecordUseCaseParams {
  final String vehicleId;
  final String recordId;
  final CompleteServiceRecordParams data;
  const CompleteServiceRecordUseCaseParams(this.vehicleId, this.recordId, this.data);
}

class CompleteServiceRecordUseCase implements UseCase<void, CompleteServiceRecordUseCaseParams> {
  final IVehicleRepository _repository;
  const CompleteServiceRecordUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(CompleteServiceRecordUseCaseParams params) =>
      _repository.completeServiceRecord(params.vehicleId, params.recordId, params.data);
}

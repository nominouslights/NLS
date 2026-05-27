import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class UpdateServiceRecordUseCaseParams {
  final String vehicleId;
  final String recordId;
  final AddServiceRecordParams data;
  const UpdateServiceRecordUseCaseParams(this.vehicleId, this.recordId, this.data);
}

class UpdateServiceRecordUseCase implements UseCase<void, UpdateServiceRecordUseCaseParams> {
  final IVehicleRepository _repository;
  const UpdateServiceRecordUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdateServiceRecordUseCaseParams params) =>
      _repository.updateServiceRecord(params.vehicleId, params.recordId, params.data);
}

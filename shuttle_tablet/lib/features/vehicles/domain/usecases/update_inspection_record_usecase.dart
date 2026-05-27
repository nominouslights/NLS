import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class UpdateInspectionRecordUseCaseParams {
  final String vehicleId;
  final String recordId;
  final AddInspectionRecordParams data;
  const UpdateInspectionRecordUseCaseParams(this.vehicleId, this.recordId, this.data);
}

class UpdateInspectionRecordUseCase implements UseCase<void, UpdateInspectionRecordUseCaseParams> {
  final IVehicleRepository _repository;
  const UpdateInspectionRecordUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdateInspectionRecordUseCaseParams params) =>
      _repository.updateInspectionRecord(params.vehicleId, params.recordId, params.data);
}

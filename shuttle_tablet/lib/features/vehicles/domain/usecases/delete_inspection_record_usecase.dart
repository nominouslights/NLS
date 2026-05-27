import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class DeleteInspectionRecordParams {
  final String vehicleId;
  final String recordId;
  const DeleteInspectionRecordParams(this.vehicleId, this.recordId);
}

class DeleteInspectionRecordUseCase implements UseCase<void, DeleteInspectionRecordParams> {
  final IVehicleRepository _repository;
  const DeleteInspectionRecordUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(DeleteInspectionRecordParams params) =>
      _repository.deleteInspectionRecord(params.vehicleId, params.recordId);
}

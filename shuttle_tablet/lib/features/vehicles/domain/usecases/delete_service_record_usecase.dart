import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class DeleteServiceRecordParams {
  final String vehicleId;
  final String recordId;
  const DeleteServiceRecordParams(this.vehicleId, this.recordId);
}

class DeleteServiceRecordUseCase implements UseCase<void, DeleteServiceRecordParams> {
  final IVehicleRepository _repository;
  const DeleteServiceRecordUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(DeleteServiceRecordParams params) =>
      _repository.deleteServiceRecord(params.vehicleId, params.recordId);
}

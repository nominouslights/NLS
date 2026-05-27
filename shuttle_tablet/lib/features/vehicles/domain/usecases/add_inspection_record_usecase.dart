import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class AddInspectionRecordUseCaseParams {
  final String vehicleId;
  final AddInspectionRecordParams data;
  const AddInspectionRecordUseCaseParams(this.vehicleId, this.data);
}

class AddInspectionRecordUseCase implements UseCase<String, AddInspectionRecordUseCaseParams> {
  final IVehicleRepository _repository;
  const AddInspectionRecordUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(AddInspectionRecordUseCaseParams params) =>
      _repository.addInspectionRecord(params.vehicleId, params.data);
}

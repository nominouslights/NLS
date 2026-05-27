import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_vehicle_repository.dart';

class AddServiceRecordUseCaseParams {
  final String vehicleId;
  final AddServiceRecordParams data;
  const AddServiceRecordUseCaseParams(this.vehicleId, this.data);
}

class AddServiceRecordUseCase implements UseCase<String, AddServiceRecordUseCaseParams> {
  final IVehicleRepository _repository;
  const AddServiceRecordUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(AddServiceRecordUseCaseParams params) =>
      _repository.addServiceRecord(params.vehicleId, params.data);
}

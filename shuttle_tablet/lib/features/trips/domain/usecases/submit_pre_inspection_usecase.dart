import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class SubmitPreInspectionUseCaseParams {
  final String tripId;
  final SubmitPreInspectionParams data;
  const SubmitPreInspectionUseCaseParams(
      {required this.tripId, required this.data});
}

class SubmitPreInspectionUseCase
    implements UseCase<void, SubmitPreInspectionUseCaseParams> {
  final ITripRepository _repository;
  const SubmitPreInspectionUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(SubmitPreInspectionUseCaseParams params) =>
      _repository.submitPreInspection(params.tripId, params.data);
}

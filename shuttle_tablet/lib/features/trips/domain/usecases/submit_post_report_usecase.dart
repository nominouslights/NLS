import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class SubmitPostReportUseCaseParams {
  final String tripId;
  final SubmitPostReportParams data;
  const SubmitPostReportUseCaseParams(
      {required this.tripId, required this.data});
}

class SubmitPostReportUseCase
    implements UseCase<void, SubmitPostReportUseCaseParams> {
  final ITripRepository _repository;
  const SubmitPostReportUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(SubmitPostReportUseCaseParams params) =>
      _repository.submitPostReport(params.tripId, params.data);
}

import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_driver_repository.dart';

class FleetRosterParams {
  final DateTime rangeStart;
  final DateTime rangeEnd;
  const FleetRosterParams(this.rangeStart, this.rangeEnd);
}

class GetFleetRosterUseCase
    implements UseCase<List<DriverRosterSummary>, FleetRosterParams> {
  final IDriverRepository _repository;
  const GetFleetRosterUseCase(this._repository);

  @override
  Future<Either<Failure, List<DriverRosterSummary>>> call(FleetRosterParams params) =>
      _repository.getFleetRoster(params.rangeStart, params.rangeEnd);
}

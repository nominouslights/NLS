import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/driver_roster_entry.dart';
import '../repositories/i_driver_repository.dart';

class GetRosterParams {
  final String driverId;
  final DateTime rangeStart;
  final DateTime rangeEnd;
  const GetRosterParams(this.driverId, this.rangeStart, this.rangeEnd);
}

class GetDriverRosterUseCase
    implements UseCase<List<DriverRosterEntry>, GetRosterParams> {
  final IDriverRepository _repository;
  const GetDriverRosterUseCase(this._repository);

  @override
  Future<Either<Failure, List<DriverRosterEntry>>> call(GetRosterParams params) =>
      _repository.getDriverRoster(params.driverId, params.rangeStart, params.rangeEnd);
}

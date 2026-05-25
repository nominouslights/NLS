import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_driver_repository.dart';

class UpsertRosterUseCaseParams {
  final String driverId;
  final UpsertRosterEntryParams params;
  const UpsertRosterUseCaseParams(this.driverId, this.params);
}

class UpsertRosterEntryUseCase
    implements UseCase<String, UpsertRosterUseCaseParams> {
  final IDriverRepository _repository;
  const UpsertRosterEntryUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(UpsertRosterUseCaseParams params) =>
      _repository.upsertRosterEntry(params.driverId, params.params);
}

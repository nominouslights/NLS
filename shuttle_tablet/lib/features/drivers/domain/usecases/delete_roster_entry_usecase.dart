import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_driver_repository.dart';

class DeleteRosterEntryParams {
  final String driverId;
  final String entryId;
  const DeleteRosterEntryParams(this.driverId, this.entryId);
}

class DeleteRosterEntryUseCase
    implements UseCase<void, DeleteRosterEntryParams> {
  final IDriverRepository _repository;
  const DeleteRosterEntryUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(DeleteRosterEntryParams params) =>
      _repository.deleteRosterEntry(params.driverId, params.entryId);
}

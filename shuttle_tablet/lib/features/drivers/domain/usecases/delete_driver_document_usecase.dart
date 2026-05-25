import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_driver_repository.dart';

class DeleteDocumentParams {
  final String driverId;
  final String documentId;
  const DeleteDocumentParams(this.driverId, this.documentId);
}

class DeleteDriverDocumentUseCase implements UseCase<void, DeleteDocumentParams> {
  final IDriverRepository _repository;
  const DeleteDriverDocumentUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(DeleteDocumentParams params) =>
      _repository.deleteDriverDocument(params.driverId, params.documentId);
}

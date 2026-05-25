import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_driver_repository.dart';

class UploadDriverDocumentUseCaseParams {
  final String driverId;
  final UploadDocumentParams params;
  const UploadDriverDocumentUseCaseParams(this.driverId, this.params);
}

class UploadDriverDocumentUseCase
    implements UseCase<String, UploadDriverDocumentUseCaseParams> {
  final IDriverRepository _repository;
  const UploadDriverDocumentUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(UploadDriverDocumentUseCaseParams params) =>
      _repository.uploadDriverDocument(params.driverId, params.params);
}

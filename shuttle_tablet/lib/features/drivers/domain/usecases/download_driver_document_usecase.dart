import 'dart:typed_data';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_driver_repository.dart';

class DownloadDocumentParams {
  final String driverId;
  final String documentId;
  const DownloadDocumentParams(this.driverId, this.documentId);
}

class DownloadDriverDocumentUseCase
    implements UseCase<Uint8List, DownloadDocumentParams> {
  final IDriverRepository _repository;
  const DownloadDriverDocumentUseCase(this._repository);

  @override
  Future<Either<Failure, Uint8List>> call(DownloadDocumentParams params) =>
      _repository.downloadDriverDocument(params.driverId, params.documentId);
}

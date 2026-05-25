import 'dart:typed_data';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/driver_document.dart';
import '../../domain/repositories/i_driver_repository.dart';
import '../../domain/usecases/delete_driver_document_usecase.dart';
import '../../domain/usecases/download_driver_document_usecase.dart';
import '../../domain/usecases/get_driver_by_id_usecase.dart';
import '../../domain/usecases/get_driver_documents_usecase.dart';
import '../../domain/usecases/upload_driver_document_usecase.dart';

final driverDocumentsProvider = AsyncNotifierProvider.family<
    DriverDocumentsNotifier, List<DriverDocument>, String>(
  DriverDocumentsNotifier.new,
);

class DriverDocumentsNotifier
    extends FamilyAsyncNotifier<List<DriverDocument>, String> {
  @override
  Future<List<DriverDocument>> build(String driverId) => _load(driverId);

  Future<List<DriverDocument>> _load(String driverId) async {
    final result = await sl<GetDriverDocumentsUseCase>()(
      DriverIdParams(driverId),
    );
    return result.fold(
      (failure) => throw Exception(failure.message),
      (docs) => docs,
    );
  }

  Future<String> uploadDocument(UploadDocumentParams params) async {
    final result = await sl<UploadDriverDocumentUseCase>()(
      UploadDriverDocumentUseCaseParams(arg, params),
    );
    return result.fold(
      (failure) => throw Exception(failure.message),
      (id) {
        ref.invalidateSelf();
        return id;
      },
    );
  }

  Future<Uint8List> downloadDocument(String documentId) async {
    final result = await sl<DownloadDriverDocumentUseCase>()(
      DownloadDocumentParams(arg, documentId),
    );
    return result.fold(
      (failure) => throw Exception(failure.message),
      (bytes) => bytes,
    );
  }

  Future<void> deleteDocument(String documentId) async {
    final result = await sl<DeleteDriverDocumentUseCase>()(
      DeleteDocumentParams(arg, documentId),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }
}

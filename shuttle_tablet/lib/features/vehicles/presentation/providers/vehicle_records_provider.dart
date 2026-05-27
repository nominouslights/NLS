import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/repositories/i_vehicle_repository.dart';
import '../../domain/usecases/add_service_record_usecase.dart';
import '../../domain/usecases/update_service_record_usecase.dart';
import '../../domain/usecases/complete_service_record_usecase.dart';
import '../../domain/usecases/delete_service_record_usecase.dart';
import '../../domain/usecases/add_inspection_record_usecase.dart';
import '../../domain/usecases/update_inspection_record_usecase.dart';
import '../../domain/usecases/delete_inspection_record_usecase.dart';
import 'vehicle_detail_provider.dart';

final vehicleRecordsProvider =
    AsyncNotifierProvider.family<VehicleRecordsNotifier, void, String>(
  VehicleRecordsNotifier.new,
);

class VehicleRecordsNotifier
    extends FamilyAsyncNotifier<void, String> {
  @override
  Future<void> build(String arg) async {}

  String get _vehicleId => arg;

  void _refresh() => ref.invalidate(vehicleDetailProvider(_vehicleId));

  // ── Service Records ────────────────────────────────────────────────────────

  Future<void> addServiceRecord(AddServiceRecordParams params) async {
    state = const AsyncLoading();
    final result = await sl<AddServiceRecordUseCase>()(
      AddServiceRecordUseCaseParams(_vehicleId, params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => _refresh(),
    );
    state = const AsyncData(null);
  }

  Future<void> updateServiceRecord(
      String recordId, AddServiceRecordParams params) async {
    state = const AsyncLoading();
    final result = await sl<UpdateServiceRecordUseCase>()(
      UpdateServiceRecordUseCaseParams(_vehicleId, recordId, params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => _refresh(),
    );
    state = const AsyncData(null);
  }

  Future<void> completeServiceRecord(
      String recordId, CompleteServiceRecordParams params) async {
    state = const AsyncLoading();
    final result = await sl<CompleteServiceRecordUseCase>()(
      CompleteServiceRecordUseCaseParams(_vehicleId, recordId, params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => _refresh(),
    );
    state = const AsyncData(null);
  }

  Future<void> deleteServiceRecord(String recordId) async {
    state = const AsyncLoading();
    final result = await sl<DeleteServiceRecordUseCase>()(
      DeleteServiceRecordParams(_vehicleId, recordId),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => _refresh(),
    );
    state = const AsyncData(null);
  }

  // ── Inspection Records ─────────────────────────────────────────────────────

  Future<void> addInspectionRecord(AddInspectionRecordParams params) async {
    state = const AsyncLoading();
    final result = await sl<AddInspectionRecordUseCase>()(
      AddInspectionRecordUseCaseParams(_vehicleId, params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => _refresh(),
    );
    state = const AsyncData(null);
  }

  Future<void> updateInspectionRecord(
      String recordId, AddInspectionRecordParams params) async {
    state = const AsyncLoading();
    final result = await sl<UpdateInspectionRecordUseCase>()(
      UpdateInspectionRecordUseCaseParams(_vehicleId, recordId, params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => _refresh(),
    );
    state = const AsyncData(null);
  }

  Future<void> deleteInspectionRecord(String recordId) async {
    state = const AsyncLoading();
    final result = await sl<DeleteInspectionRecordUseCase>()(
      DeleteInspectionRecordParams(_vehicleId, recordId),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => _refresh(),
    );
    state = const AsyncData(null);
  }
}

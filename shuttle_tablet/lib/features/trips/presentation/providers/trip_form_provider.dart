import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../../domain/usecases/assign_driver_usecase.dart';
import '../../domain/usecases/create_trip_usecase.dart';
import '../../domain/usecases/dispatch_trip_usecase.dart';
import '../../domain/usecases/get_trip_by_id_usecase.dart';
import '../../domain/usecases/submit_post_report_usecase.dart';
import '../../domain/usecases/submit_pre_inspection_usecase.dart';
import '../../domain/usecases/update_trip_usecase.dart';

final tripFormProvider =
    AsyncNotifierProvider<TripFormNotifier, void>(TripFormNotifier.new);

class TripFormNotifier extends AsyncNotifier<void> {
  @override
  Future<void> build() async {}

  Future<String> createTrip(CreateTripParams params) async {
    state = const AsyncLoading();
    final result = await sl<CreateTripUseCase>()(params);
    return result.fold(
      (failure) {
        state = AsyncError(Exception(failure.message), StackTrace.current);
        throw Exception(failure.message);
      },
      (id) {
        state = const AsyncData(null);
        return id;
      },
    );
  }

  Future<void> updateTrip(String id, UpdateTripParams params) async {
    state = const AsyncLoading();
    final result = await sl<UpdateTripUseCase>()(
      UpdateTripUseCaseParams(id: id, data: params),
    );
    result.fold(
      (failure) {
        state = AsyncError(Exception(failure.message), StackTrace.current);
        throw Exception(failure.message);
      },
      (_) => state = const AsyncData(null),
    );
  }

  Future<void> assignDriver(String tripId, AssignDriverParams params) async {
    state = const AsyncLoading();
    final result = await sl<AssignDriverUseCase>()(
      AssignDriverUseCaseParams(tripId: tripId, data: params),
    );
    result.fold(
      (failure) {
        state = AsyncError(Exception(failure.message), StackTrace.current);
        throw Exception(failure.message);
      },
      (_) => state = const AsyncData(null),
    );
  }

  Future<void> dispatchTrip(String tripId) async {
    state = const AsyncLoading();
    final result =
        await sl<DispatchTripUseCase>()(TripIdParams(tripId));
    result.fold(
      (failure) {
        state = AsyncError(Exception(failure.message), StackTrace.current);
        throw Exception(failure.message);
      },
      (_) => state = const AsyncData(null),
    );
  }

  Future<void> submitPreInspection(
      String tripId, SubmitPreInspectionParams params) async {
    state = const AsyncLoading();
    final result = await sl<SubmitPreInspectionUseCase>()(
      SubmitPreInspectionUseCaseParams(tripId: tripId, data: params),
    );
    result.fold(
      (failure) {
        state = AsyncError(Exception(failure.message), StackTrace.current);
        throw Exception(failure.message);
      },
      (_) => state = const AsyncData(null),
    );
  }

  Future<void> submitPostReport(
      String tripId, SubmitPostReportParams params) async {
    state = const AsyncLoading();
    final result = await sl<SubmitPostReportUseCase>()(
      SubmitPostReportUseCaseParams(tripId: tripId, data: params),
    );
    result.fold(
      (failure) {
        state = AsyncError(Exception(failure.message), StackTrace.current);
        throw Exception(failure.message);
      },
      (_) {
        state = const AsyncData(null);
      },
    );
  }
}

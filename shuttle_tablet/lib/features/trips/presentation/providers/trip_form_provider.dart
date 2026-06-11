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

/// Imperative trip mutations. State is not watched by UI — callers handle errors.
final tripFormProvider = Provider<TripFormActions>((ref) => TripFormActions());

class TripFormActions {
  Future<String> createTrip(CreateTripParams params) async {
    final result = await sl<CreateTripUseCase>()(params);
    return result.fold(
      (failure) => throw Exception(failure.message),
      (id) => id,
    );
  }

  Future<void> updateTrip(String id, UpdateTripParams params) async {
    final result = await sl<UpdateTripUseCase>()(
      UpdateTripUseCaseParams(id: id, data: params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) {},
    );
  }

  Future<void> assignDriver(String tripId, AssignDriverParams params) async {
    final result = await sl<AssignDriverUseCase>()(
      AssignDriverUseCaseParams(tripId: tripId, data: params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) {},
    );
  }

  Future<void> dispatchTrip(String tripId) async {
    final result = await sl<DispatchTripUseCase>()(TripIdParams(tripId));
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) {},
    );
  }

  Future<void> submitPreInspection(
      String tripId, SubmitPreInspectionParams params) async {
    final result = await sl<SubmitPreInspectionUseCase>()(
      SubmitPreInspectionUseCaseParams(tripId: tripId, data: params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) {},
    );
  }

  Future<void> submitPostReport(
      String tripId, SubmitPostReportParams params) async {
    final result = await sl<SubmitPostReportUseCase>()(
      SubmitPostReportUseCaseParams(tripId: tripId, data: params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) {},
    );
  }
}

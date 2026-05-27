import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/repositories/i_vehicle_repository.dart';
import '../../domain/usecases/create_vehicle_usecase.dart';
import '../../domain/usecases/update_vehicle_usecase.dart';

final vehicleFormProvider =
    AsyncNotifierProvider<VehicleFormNotifier, void>(VehicleFormNotifier.new);

class VehicleFormNotifier extends AsyncNotifier<void> {
  @override
  Future<void> build() async {}

  Future<void> createVehicle(CreateVehicleParams params) async {
    state = const AsyncLoading();
    final result = await sl<CreateVehicleUseCase>()(params);
    state = result.fold(
      (failure) => AsyncError(Exception(failure.message), StackTrace.current),
      (_) => const AsyncData(null),
    );
    if (state.hasError) throw state.error!;
  }

  Future<void> updateVehicle(String id, UpdateVehicleParams params) async {
    state = const AsyncLoading();
    final result =
        await sl<UpdateVehicleUseCase>()(UpdateVehicleParams2(id, params));
    state = result.fold(
      (failure) => AsyncError(Exception(failure.message), StackTrace.current),
      (_) => const AsyncData(null),
    );
    if (state.hasError) throw state.error!;
  }
}

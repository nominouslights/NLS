import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/usecases/usecase.dart';
import '../../domain/entities/vehicle.dart';
import '../../domain/usecases/delete_vehicle_usecase.dart';
import '../../domain/usecases/get_vehicles_usecase.dart';
import '../../domain/usecases/set_vehicle_status_usecase.dart';
import '../../domain/usecases/set_vehicle_out_of_service_usecase.dart';

final vehiclesProvider =
    AsyncNotifierProvider<VehiclesNotifier, List<Vehicle>>(VehiclesNotifier.new);

class VehiclesNotifier extends AsyncNotifier<List<Vehicle>> {
  @override
  Future<List<Vehicle>> build() => _load();

  Future<List<Vehicle>> _load() async {
    final result = await sl<GetVehiclesUseCase>()(const NoParams());
    return result.fold(
      (failure) => throw Exception(failure.message),
      (vehicles) => vehicles,
    );
  }

  Future<void> deleteVehicle(String id) async {
    final result =
        await sl<DeleteVehicleUseCase>()(VehicleIdParams(id));
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> setStatus(String id, String status, {String? statusNote}) async {
    final result = await sl<SetVehicleStatusUseCase>()(
      SetVehicleStatusParams(id, status, statusNote: statusNote),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> setOutOfService(String id, String reason) async {
    final result = await sl<SetVehicleOutOfServiceUseCase>()(
      SetOutOfServiceParams(id, reason),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> refresh() => ref.refresh(vehiclesProvider.future);
}

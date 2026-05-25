import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/usecases/usecase.dart';
import '../../domain/entities/driver.dart';
import '../../domain/usecases/delete_driver_usecase.dart';
import '../../domain/usecases/get_driver_by_id_usecase.dart';
import '../../domain/usecases/get_drivers_usecase.dart';
import '../../domain/usecases/set_driver_status_usecase.dart';

final driversProvider =
    AsyncNotifierProvider<DriversNotifier, List<Driver>>(DriversNotifier.new);

class DriversNotifier extends AsyncNotifier<List<Driver>> {
  @override
  Future<List<Driver>> build() => _load();

  Future<List<Driver>> _load() async {
    final result = await sl<GetDriversUseCase>()(const NoParams());
    return result.fold(
      (failure) => throw Exception(failure.message),
      (drivers) => drivers,
    );
  }

  Future<void> deleteDriver(String id) async {
    final result = await sl<DeleteDriverUseCase>()(DriverIdParams(id));
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> setStatus(String id, DriverStatus status) async {
    final result = await sl<SetDriverStatusUseCase>()(
      SetDriverStatusParams(id, status),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> refresh() => ref.refresh(driversProvider.future);
}

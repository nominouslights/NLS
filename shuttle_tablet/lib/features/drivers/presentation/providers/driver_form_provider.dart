import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/repositories/i_driver_repository.dart';
import '../../domain/usecases/create_driver_usecase.dart';
import '../../domain/usecases/update_driver_usecase.dart';

final driverFormProvider =
    AsyncNotifierProvider<DriverFormNotifier, void>(DriverFormNotifier.new);

class DriverFormNotifier extends AsyncNotifier<void> {
  @override
  Future<void> build() async {}

  Future<String> createDriver(CreateDriverParams params) async {
    state = const AsyncLoading();
    final result = await sl<CreateDriverUseCase>()(params);
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

  Future<void> updateDriver(String id, UpdateDriverParams params) async {
    state = const AsyncLoading();
    final result = await sl<UpdateDriverUseCase>()(
      UpdateDriverUseCaseParams(id, params),
    );
    result.fold(
      (failure) {
        state = AsyncError(Exception(failure.message), StackTrace.current);
        throw Exception(failure.message);
      },
      (_) => state = const AsyncData(null),
    );
  }
}

import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/usecases/register_usecase.dart';

final registerProvider =
    AsyncNotifierProvider<RegisterNotifier, void>(RegisterNotifier.new);

class RegisterNotifier extends AsyncNotifier<void> {
  @override
  Future<void> build() async {}

  Future<bool> register(String email, String password) async {
    state = const AsyncLoading();
    final result = await sl<RegisterUseCase>()(
      RegisterParams(email: email, password: password, role: 'Driver'),
    );
    return result.match(
      (failure) {
        state = AsyncError(Exception(failure.message), StackTrace.current);
        return false;
      },
      (_) {
        state = const AsyncData(null);
        return true;
      },
    );
  }
}

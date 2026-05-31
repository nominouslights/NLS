import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/usecases/usecase.dart';
import '../../domain/usecases/get_setup_status_usecase.dart';
import '../../domain/usecases/initialize_system_usecase.dart';

final setupStatusProvider = FutureProvider<bool>((ref) async {
  final result = await sl<GetSetupStatusUseCase>()(const NoParams());
  return result.fold((_) => true, (isSetupComplete) => isSetupComplete);
});

final initializeSystemProvider =
    AsyncNotifierProvider<InitializeSystemNotifier, void>(InitializeSystemNotifier.new);

class InitializeSystemNotifier extends AsyncNotifier<void> {
  @override
  Future<void> build() async {}

  Future<String?> initialize(String email, String password) async {
    state = const AsyncLoading();
    final result = await sl<InitializeSystemUseCase>()(
      InitializeParams(email: email, password: password),
    );
    return result.fold(
      (failure) {
        state = const AsyncData(null);
        return failure.message;
      },
      (_) {
        state = const AsyncData(null);
        ref.invalidate(setupStatusProvider);
        return null;
      },
    );
  }
}

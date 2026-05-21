import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/storage/secure_storage_service.dart';
import '../../domain/usecases/login_usecase.dart';

final authProvider = AsyncNotifierProvider<AuthNotifier, String?>(AuthNotifier.new);

final userRoleProvider = FutureProvider<String?>((ref) async {
  ref.watch(authProvider);
  return sl<SecureStorageService>().getRole();
});

class AuthNotifier extends AsyncNotifier<String?> {
  @override
  Future<String?> build() async {
    return sl<SecureStorageService>().getAccessToken();
  }

  Future<void> login(String email, String password) async {
    state = const AsyncLoading();
    try {
      final result = await sl<LoginUseCase>()(
        LoginParams(email: email, password: password),
      );
      await result.match(
        (failure) async {
          state = AsyncError(Exception(failure.message), StackTrace.current);
        },
        (token) async {
          final storage = sl<SecureStorageService>();
          await storage.saveAccessToken(token.accessToken);
          await storage.saveRefreshToken(token.refreshToken);
          await storage.saveRole(token.role);
          state = AsyncData(token.accessToken);
        },
      );
    } catch (e, st) {
      state = AsyncError(e, st);
    }
  }

  Future<void> logout() async {
    await sl<SecureStorageService>().clearAll();
    state = const AsyncData(null);
  }
}

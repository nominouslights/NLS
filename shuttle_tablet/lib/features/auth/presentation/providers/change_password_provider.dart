import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/storage/secure_storage_service.dart';
import '../../domain/usecases/change_password_usecase.dart';
import 'auth_provider.dart';

final changePasswordProvider =
    AsyncNotifierProvider<ChangePasswordNotifier, void>(ChangePasswordNotifier.new);

class ChangePasswordNotifier extends AsyncNotifier<void> {
  @override
  Future<void> build() async {}

  Future<String?> changePassword(String currentPassword, String newPassword) async {
    state = const AsyncLoading();
    final result = await sl<ChangePasswordUseCase>()(
      ChangePasswordParams(
        currentPassword: currentPassword,
        newPassword: newPassword,
      ),
    );
    return result.fold(
      (failure) {
        state = const AsyncData(null);
        return failure.message;
      },
      (_) async {
        await sl<SecureStorageService>().saveMustChangePassword(false);
        state = const AsyncData(null);
        ref.invalidate(mustChangePasswordProvider);
        return null;
      },
    );
  }
}

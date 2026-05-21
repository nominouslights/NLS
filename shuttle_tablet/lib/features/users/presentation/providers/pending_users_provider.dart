import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/usecases/usecase.dart';
import '../../domain/entities/pending_user.dart';
import '../../domain/usecases/approve_user_usecase.dart';
import '../../domain/usecases/get_pending_users_usecase.dart';
import '../../domain/usecases/reject_user_usecase.dart';

final pendingUsersProvider =
    AsyncNotifierProvider<PendingUsersNotifier, List<PendingUser>>(
        PendingUsersNotifier.new);

class PendingUsersNotifier extends AsyncNotifier<List<PendingUser>> {
  @override
  Future<List<PendingUser>> build() => _load();

  Future<List<PendingUser>> _load() async {
    final result = await sl<GetPendingUsersUseCase>()(const NoParams());
    return result.fold(
      (failure) => throw Exception(failure.message),
      (users) => users,
    );
  }

  Future<void> approve(String id) async {
    final result = await sl<ApproveUserUseCase>()(UserIdParams(id));
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> reject(String id) async {
    final result = await sl<RejectUserUseCase>()(UserIdParams(id));
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }
}

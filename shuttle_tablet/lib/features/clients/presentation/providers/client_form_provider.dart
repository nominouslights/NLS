import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/repositories/i_client_repository.dart';
import '../../domain/usecases/create_client_usecase.dart';
import '../../domain/usecases/update_client_usecase.dart';

final clientFormProvider =
    AsyncNotifierProvider<ClientFormNotifier, void>(ClientFormNotifier.new);

class ClientFormNotifier extends AsyncNotifier<void> {
  @override
  Future<void> build() async {}

  Future<String> createClient(CreateClientParams params) async {
    state = const AsyncLoading();
    final result = await sl<CreateClientUseCase>()(params);
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

  Future<void> updateClient(String id, UpdateClientParams params) async {
    state = const AsyncLoading();
    final result = await sl<UpdateClientUseCase>()(
      UpdateClientUseCaseParams(id: id, data: params),
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

import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/usecases/usecase.dart';
import '../../domain/entities/client.dart';
import '../../domain/usecases/delete_client_usecase.dart';
import '../../domain/usecases/get_client_by_id_usecase.dart';
import '../../domain/usecases/get_clients_usecase.dart';

final clientsProvider =
    AsyncNotifierProvider<ClientsNotifier, List<Client>>(ClientsNotifier.new);

class ClientsNotifier extends AsyncNotifier<List<Client>> {
  @override
  Future<List<Client>> build() => _load();

  Future<List<Client>> _load() async {
    final result = await sl<GetClientsUseCase>()(const NoParams());
    return result.fold(
      (failure) => throw Exception(failure.message),
      (clients) => clients,
    );
  }

  Future<void> deleteClient(String id) async {
    final result = await sl<DeleteClientUseCase>()(ClientIdParams(id));
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> refresh() => ref.refresh(clientsProvider.future);
}

import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/client.dart';
import '../../domain/usecases/get_client_by_id_usecase.dart';

final clientDetailProvider =
    FutureProvider.family<Client, String>((ref, clientId) async {
  final result = await sl<GetClientByIdUseCase>()(ClientIdParams(clientId));
  return result.fold(
    (failure) => throw Exception(failure.message),
    (client) => client,
  );
});

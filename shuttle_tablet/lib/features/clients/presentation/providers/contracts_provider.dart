import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/contract.dart';
import '../../domain/repositories/i_contract_repository.dart';
import '../../domain/usecases/add_rate_line_usecase.dart';
import '../../domain/usecases/create_contract_usecase.dart';
import '../../domain/usecases/delete_rate_line_usecase.dart';
import '../../domain/usecases/get_client_by_id_usecase.dart';
import '../../domain/usecases/get_contracts_by_client_usecase.dart';

final contractsProvider = AsyncNotifierProviderFamily<ContractsNotifier, List<Contract>, String>(
  ContractsNotifier.new,
);

class ContractsNotifier extends FamilyAsyncNotifier<List<Contract>, String> {
  @override
  Future<List<Contract>> build(String clientId) => _load(clientId);

  Future<List<Contract>> _load(String clientId) async {
    final result = await sl<GetContractsByClientUseCase>()(ClientIdParams(clientId));
    return result.fold(
      (failure) => throw Exception(failure.message),
      (contracts) => contracts,
    );
  }

  Future<void> createContract(CreateContractParams params) async {
    final result = await sl<CreateContractUseCase>()(params);
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> addRateLine(AddRateLineParams params) async {
    final result = await sl<AddRateLineUseCase>()(params);
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> deleteRateLine(String rateLineId, String clientId) async {
    final result = await sl<DeleteRateLineUseCase>()(
      DeleteRateLineParams(rateLineId: rateLineId, clientId: clientId),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }
}

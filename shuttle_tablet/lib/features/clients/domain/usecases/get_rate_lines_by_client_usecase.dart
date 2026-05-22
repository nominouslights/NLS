import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/contract_rate_line.dart';
import '../repositories/i_contract_repository.dart';
import 'get_client_by_id_usecase.dart';

class GetRateLinesByClientUseCase implements UseCase<List<ContractRateLine>, ClientIdParams> {
  final IContractRepository _repository;
  const GetRateLinesByClientUseCase(this._repository);

  @override
  Future<Either<Failure, List<ContractRateLine>>> call(ClientIdParams params) =>
      _repository.getRateLinesByClientId(params.id);
}

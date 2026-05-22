import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/contract.dart';
import '../repositories/i_contract_repository.dart';
import 'get_client_by_id_usecase.dart';

class GetContractsByClientUseCase implements UseCase<List<Contract>, ClientIdParams> {
  final IContractRepository _repository;
  const GetContractsByClientUseCase(this._repository);

  @override
  Future<Either<Failure, List<Contract>>> call(ClientIdParams params) =>
      _repository.getContractsByClientId(params.id);
}

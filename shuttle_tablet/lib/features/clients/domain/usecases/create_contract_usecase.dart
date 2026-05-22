import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_contract_repository.dart';

class CreateContractUseCase implements UseCase<String, CreateContractParams> {
  final IContractRepository _repository;
  const CreateContractUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(CreateContractParams params) =>
      _repository.createContract(params);
}

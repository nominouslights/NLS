import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_contract_repository.dart';

class AddRateLineUseCase implements UseCase<String, AddRateLineParams> {
  final IContractRepository _repository;
  const AddRateLineUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(AddRateLineParams params) =>
      _repository.addRateLine(params);
}

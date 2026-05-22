import 'package:equatable/equatable.dart';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_contract_repository.dart';

class DeleteRateLineUseCase implements UseCase<void, DeleteRateLineParams> {
  final IContractRepository _repository;
  const DeleteRateLineUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(DeleteRateLineParams params) =>
      _repository.deleteRateLine(params.rateLineId, params.clientId);
}

class DeleteRateLineParams extends Equatable {
  final String rateLineId;
  final String clientId;
  const DeleteRateLineParams({required this.rateLineId, required this.clientId});

  @override
  List<Object?> get props => [rateLineId, clientId];
}

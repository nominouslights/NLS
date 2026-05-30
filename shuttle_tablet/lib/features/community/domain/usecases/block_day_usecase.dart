import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_community_repository.dart';

class BlockDayUseCase implements UseCase<int, BlockDayParams> {
  final ICommunityRepository _repository;
  const BlockDayUseCase(this._repository);

  @override
  Future<Either<Failure, int>> call(BlockDayParams params) =>
      _repository.blockDay(params);
}

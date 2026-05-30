import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_community_repository.dart';

class UnblockDayUseCase implements UseCase<void, String> {
  final ICommunityRepository _repository;
  const UnblockDayUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(String date) =>
      _repository.unblockDay(date);
}

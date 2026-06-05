import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_trip_repository.dart';

class AddCargoItemUseCase implements UseCase<String, AddCargoItemParams> {
  final ITripRepository _repository;
  const AddCargoItemUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(AddCargoItemParams params) =>
      _repository.addCargoItem(params);
}

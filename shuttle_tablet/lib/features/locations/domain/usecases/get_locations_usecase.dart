import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/saved_location.dart';
import '../repositories/i_location_repository.dart';

class GetLocationsUseCase implements UseCase<List<SavedLocation>, NoParams> {
  final ILocationRepository _repository;
  const GetLocationsUseCase(this._repository);

  @override
  Future<Either<Failure, List<SavedLocation>>> call(NoParams params) =>
      _repository.getLocations();
}

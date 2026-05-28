import 'package:equatable/equatable.dart';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_location_repository.dart';

class UpdateLocationUseCase implements UseCase<void, UpdateLocationUseCaseParams> {
  final ILocationRepository _repository;
  const UpdateLocationUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdateLocationUseCaseParams params) =>
      _repository.updateLocation(params.id, params.data);
}

class UpdateLocationUseCaseParams extends Equatable {
  final String id;
  final UpdateLocationParams data;
  const UpdateLocationUseCaseParams({required this.id, required this.data});

  @override
  List<Object?> get props => [id, data];
}

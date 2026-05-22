import 'package:equatable/equatable.dart';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_client_repository.dart';

class UpdateClientUseCase implements UseCase<void, UpdateClientUseCaseParams> {
  final IClientRepository _repository;
  const UpdateClientUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdateClientUseCaseParams params) =>
      _repository.updateClient(params.id, params.data);
}

class UpdateClientUseCaseParams extends Equatable {
  final String id;
  final UpdateClientParams data;
  const UpdateClientUseCaseParams({required this.id, required this.data});

  @override
  List<Object?> get props => [id, data];
}

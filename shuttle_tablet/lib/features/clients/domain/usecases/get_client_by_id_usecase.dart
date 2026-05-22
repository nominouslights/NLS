import 'package:equatable/equatable.dart';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/client.dart';
import '../repositories/i_client_repository.dart';

class GetClientByIdUseCase implements UseCase<Client, ClientIdParams> {
  final IClientRepository _repository;
  const GetClientByIdUseCase(this._repository);

  @override
  Future<Either<Failure, Client>> call(ClientIdParams params) =>
      _repository.getClientById(params.id);
}

class ClientIdParams extends Equatable {
  final String id;
  const ClientIdParams(this.id);

  @override
  List<Object?> get props => [id];
}

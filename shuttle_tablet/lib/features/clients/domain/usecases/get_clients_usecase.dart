import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/client.dart';
import '../repositories/i_client_repository.dart';

class GetClientsUseCase implements UseCase<List<Client>, NoParams> {
  final IClientRepository _repository;
  const GetClientsUseCase(this._repository);

  @override
  Future<Either<Failure, List<Client>>> call(NoParams params) =>
      _repository.getClients();
}

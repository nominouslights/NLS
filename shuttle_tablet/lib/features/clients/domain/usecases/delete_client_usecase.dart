import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_client_repository.dart';
import 'get_client_by_id_usecase.dart';

class DeleteClientUseCase implements UseCase<void, ClientIdParams> {
  final IClientRepository _repository;
  const DeleteClientUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(ClientIdParams params) =>
      _repository.deleteClient(params.id);
}

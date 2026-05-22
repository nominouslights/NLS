import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_client_repository.dart';

class CreateClientUseCase implements UseCase<String, CreateClientParams> {
  final IClientRepository _repository;
  const CreateClientUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(CreateClientParams params) =>
      _repository.createClient(params);
}

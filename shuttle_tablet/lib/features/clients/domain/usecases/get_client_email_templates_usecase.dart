import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/client_email_template.dart';
import '../repositories/i_client_repository.dart';

class GetClientEmailTemplatesUseCase
    implements UseCase<List<ClientEmailTemplate>, String> {
  final IClientRepository _repository;
  const GetClientEmailTemplatesUseCase(this._repository);

  @override
  Future<Either<Failure, List<ClientEmailTemplate>>> call(String clientId) =>
      _repository.getEmailTemplates(clientId);
}

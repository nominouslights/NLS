import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_client_repository.dart';

class UpsertClientEmailTemplateUseCase
    implements UseCase<void, UpsertEmailTemplateParams> {
  final IClientRepository _repository;
  const UpsertClientEmailTemplateUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpsertEmailTemplateParams params) =>
      _repository.upsertEmailTemplate(params);
}

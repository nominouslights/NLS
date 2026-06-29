import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/invoice.dart';
import '../repositories/i_invoice_repository.dart';

class GetInvoicesByClientParams {
  final String clientId;
  final String? status;
  const GetInvoicesByClientParams({required this.clientId, this.status});
}

class GetInvoicesByClientUseCase {
  final IInvoiceRepository _repository;
  const GetInvoicesByClientUseCase(this._repository);

  Future<Either<Failure, List<Invoice>>> call(
    GetInvoicesByClientParams params,
  ) => _repository.getByClientId(params.clientId, status: params.status);
}

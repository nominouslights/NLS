import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../repositories/i_invoice_repository.dart';

class VoidInvoiceUseCase {
  final IInvoiceRepository _repository;
  const VoidInvoiceUseCase(this._repository);

  Future<Either<Failure, Unit>> call(String invoiceId) =>
      _repository.voidInvoice(invoiceId);
}

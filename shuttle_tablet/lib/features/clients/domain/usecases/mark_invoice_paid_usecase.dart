import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../repositories/i_invoice_repository.dart';

class MarkInvoicePaidUseCase {
  final IInvoiceRepository _repository;
  const MarkInvoicePaidUseCase(this._repository);

  Future<Either<Failure, Unit>> call(String invoiceId) =>
      _repository.markPaid(invoiceId);
}

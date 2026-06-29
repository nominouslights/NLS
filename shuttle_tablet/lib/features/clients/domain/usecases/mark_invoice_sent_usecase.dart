import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../repositories/i_invoice_repository.dart';

class MarkInvoiceSentUseCase {
  final IInvoiceRepository _repository;
  const MarkInvoiceSentUseCase(this._repository);

  Future<Either<Failure, Unit>> call(String invoiceId) =>
      _repository.markSent(invoiceId);
}

import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/invoice.dart';
import '../repositories/i_invoice_repository.dart';

class GetInvoiceByIdUseCase {
  final IInvoiceRepository _repository;
  const GetInvoiceByIdUseCase(this._repository);

  Future<Either<Failure, Invoice>> call(String invoiceId) =>
      _repository.getById(invoiceId);
}

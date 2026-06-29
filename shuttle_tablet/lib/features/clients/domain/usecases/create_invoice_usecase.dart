import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/invoice.dart';
import '../repositories/i_invoice_repository.dart';

class CreateInvoiceParams {
  final String clientId;
  final List<String> tripIds;
  final String? notes;

  const CreateInvoiceParams({
    required this.clientId,
    required this.tripIds,
    this.notes,
  });
}

class CreateInvoiceUseCase {
  final IInvoiceRepository _repository;
  const CreateInvoiceUseCase(this._repository);

  Future<Either<Failure, Invoice>> call(CreateInvoiceParams params) =>
      _repository.create(params.clientId, params.tripIds, params.notes);
}

import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/billing_ready_trip.dart';
import '../repositories/i_invoice_repository.dart';

class GetBillingReadyTripsUseCase {
  final IInvoiceRepository _repository;
  const GetBillingReadyTripsUseCase(this._repository);

  Future<Either<Failure, List<BillingReadyTrip>>> call(String clientId) =>
      _repository.getBillingReadyTrips(clientId);
}

import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/billing_ready_trip.dart';
import '../entities/invoice.dart';

abstract interface class IInvoiceRepository {
  Future<Either<Failure, List<Invoice>>> getByClientId(
    String clientId, {
    String? status,
  });
  Future<Either<Failure, Invoice>> getById(String invoiceId);
  Future<Either<Failure, Invoice>> create(
    String clientId,
    List<String> tripIds,
    String? notes,
  );
  Future<Either<Failure, Unit>> markSent(String invoiceId);
  Future<Either<Failure, Unit>> markPaid(String invoiceId);
  Future<Either<Failure, Unit>> voidInvoice(String invoiceId);
  Future<Either<Failure, List<BillingReadyTrip>>> getBillingReadyTrips(
    String clientId,
  );
  Future<Either<Failure, Unit>> updateBillingRates(
    String clientId, {
    double? oneWayRate,
    double? roundTripRate,
    double? cargoRatePerKg,
  });
}

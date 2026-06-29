import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../repositories/i_invoice_repository.dart';

class UpdateClientBillingRatesParams {
  final String clientId;
  final double? oneWayRate;
  final double? roundTripRate;
  final double? cargoRatePerKg;

  const UpdateClientBillingRatesParams({
    required this.clientId,
    this.oneWayRate,
    this.roundTripRate,
    this.cargoRatePerKg,
  });
}

class UpdateClientBillingRatesUseCase {
  final IInvoiceRepository _repository;
  const UpdateClientBillingRatesUseCase(this._repository);

  Future<Either<Failure, Unit>> call(
    UpdateClientBillingRatesParams params,
  ) => _repository.updateBillingRates(
        params.clientId,
        oneWayRate: params.oneWayRate,
        roundTripRate: params.roundTripRate,
        cargoRatePerKg: params.cargoRatePerKg,
      );
}

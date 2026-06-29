import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/billing_ready_trip.dart';
import '../../domain/entities/invoice.dart';
import '../../domain/repositories/i_invoice_repository.dart';
import '../datasources/invoice_remote_datasource.dart';

class InvoiceRepositoryImpl implements IInvoiceRepository {
  final IInvoiceRemoteDataSource _dataSource;
  const InvoiceRepositoryImpl(this._dataSource);

  @override
  Future<Either<Failure, List<Invoice>>> getByClientId(
    String clientId, {
    String? status,
  }) async {
    try {
      final result = await _dataSource.getByClientId(clientId, status: status);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Invoice>> getById(String invoiceId) async {
    try {
      final result = await _dataSource.getById(invoiceId);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Invoice>> create(
    String clientId,
    List<String> tripIds,
    String? notes,
  ) async {
    try {
      final result = await _dataSource.create(clientId, tripIds, notes);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Unit>> markSent(String invoiceId) async {
    try {
      await _dataSource.markSent(invoiceId);
      return const Right(unit);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Unit>> markPaid(String invoiceId) async {
    try {
      await _dataSource.markPaid(invoiceId);
      return const Right(unit);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Unit>> voidInvoice(String invoiceId) async {
    try {
      await _dataSource.voidInvoice(invoiceId);
      return const Right(unit);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, List<BillingReadyTrip>>> getBillingReadyTrips(
    String clientId,
  ) async {
    try {
      final result = await _dataSource.getBillingReadyTrips(clientId);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Unit>> updateBillingRates(
    String clientId, {
    double? oneWayRate,
    double? roundTripRate,
    double? cargoRatePerKg,
  }) async {
    try {
      await _dataSource.updateBillingRates(
        clientId,
        oneWayRate: oneWayRate,
        roundTripRate: roundTripRate,
        cargoRatePerKg: cargoRatePerKg,
      );
      return const Right(unit);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }
}

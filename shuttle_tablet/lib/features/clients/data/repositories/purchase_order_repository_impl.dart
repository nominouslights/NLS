import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/purchase_order.dart';
import '../../domain/repositories/i_purchase_order_repository.dart';
import '../datasources/purchase_order_remote_datasource.dart';

class PurchaseOrderRepositoryImpl implements IPurchaseOrderRepository {
  final IPurchaseOrderRemoteDataSource _remoteDataSource;
  const PurchaseOrderRepositoryImpl(this._remoteDataSource);

  @override
  Future<Either<Failure, List<PurchaseOrder>>> getPurchaseOrdersByClientId(
      String clientId) async {
    try {
      final result = await _remoteDataSource.getPurchaseOrdersByClientId(clientId);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, PurchaseOrder>> getPurchaseOrderById(
      String clientId, String id) async {
    try {
      final result = await _remoteDataSource.getPurchaseOrderById(clientId, id);
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
  Future<Either<Failure, String>> createPurchaseOrder(
      CreatePurchaseOrderParams params) async {
    try {
      final id = await _remoteDataSource.createPurchaseOrder(params);
      return Right(id);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> updatePurchaseOrder(
      String id, UpdatePurchaseOrderParams params) async {
    try {
      await _remoteDataSource.updatePurchaseOrder(id, params);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }
}

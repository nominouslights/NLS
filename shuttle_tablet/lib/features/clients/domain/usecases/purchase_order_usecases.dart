import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/purchase_order.dart';
import '../repositories/i_purchase_order_repository.dart';
import 'get_client_by_id_usecase.dart';

class GetPurchaseOrdersByClientUseCase
    implements UseCase<List<PurchaseOrder>, ClientIdParams> {
  final IPurchaseOrderRepository _repository;
  const GetPurchaseOrdersByClientUseCase(this._repository);

  @override
  Future<Either<Failure, List<PurchaseOrder>>> call(ClientIdParams params) =>
      _repository.getPurchaseOrdersByClientId(params.id);
}

class GetPurchaseOrderByIdParams {
  final String clientId;
  final String purchaseOrderId;
  const GetPurchaseOrderByIdParams({
    required this.clientId,
    required this.purchaseOrderId,
  });
}

class GetPurchaseOrderByIdUseCase
    implements UseCase<PurchaseOrder, GetPurchaseOrderByIdParams> {
  final IPurchaseOrderRepository _repository;
  const GetPurchaseOrderByIdUseCase(this._repository);

  @override
  Future<Either<Failure, PurchaseOrder>> call(GetPurchaseOrderByIdParams params) =>
      _repository.getPurchaseOrderById(params.clientId, params.purchaseOrderId);
}

class CreatePurchaseOrderUseCase implements UseCase<String, CreatePurchaseOrderParams> {
  final IPurchaseOrderRepository _repository;
  const CreatePurchaseOrderUseCase(this._repository);

  @override
  Future<Either<Failure, String>> call(CreatePurchaseOrderParams params) =>
      _repository.createPurchaseOrder(params);
}

class UpdatePurchaseOrderUseCase implements UseCase<void, UpdatePurchaseOrderRequest> {
  final IPurchaseOrderRepository _repository;
  const UpdatePurchaseOrderUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(UpdatePurchaseOrderRequest params) =>
      _repository.updatePurchaseOrder(params.purchaseOrderId, params.params);
}

class UpdatePurchaseOrderRequest {
  final String purchaseOrderId;
  final UpdatePurchaseOrderParams params;
  const UpdatePurchaseOrderRequest({
    required this.purchaseOrderId,
    required this.params,
  });
}

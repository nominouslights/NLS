import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/purchase_order.dart';

abstract interface class IPurchaseOrderRepository {
  Future<Either<Failure, List<PurchaseOrder>>> getPurchaseOrdersByClientId(String clientId);
  Future<Either<Failure, PurchaseOrder>> getPurchaseOrderById(String clientId, String id);
  Future<Either<Failure, String>> createPurchaseOrder(CreatePurchaseOrderParams params);
  Future<Either<Failure, void>> updatePurchaseOrder(String id, UpdatePurchaseOrderParams params);
}

class PurchaseOrderLineItemParams {
  final String description;
  final double unitRate;
  final double quantity;

  const PurchaseOrderLineItemParams({
    required this.description,
    required this.unitRate,
    required this.quantity,
  });
}

class CreatePurchaseOrderParams {
  final String clientId;
  final String poNumber;
  final DateTime startDate;
  final String? details;
  final List<PurchaseOrderLineItemParams> lineItems;
  final List<String> contractIds;

  const CreatePurchaseOrderParams({
    required this.clientId,
    required this.poNumber,
    required this.startDate,
    this.details,
    required this.lineItems,
    this.contractIds = const [],
  });
}

class UpdatePurchaseOrderParams {
  final String clientId;
  final String poNumber;
  final DateTime startDate;
  final String? details;
  final List<PurchaseOrderLineItemParams> lineItems;
  final List<String> contractIds;

  const UpdatePurchaseOrderParams({
    required this.clientId,
    required this.poNumber,
    required this.startDate,
    this.details,
    required this.lineItems,
    this.contractIds = const [],
  });
}

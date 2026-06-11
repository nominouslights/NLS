import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/purchase_order.dart';
import '../../domain/repositories/i_purchase_order_repository.dart';
import '../../domain/usecases/get_client_by_id_usecase.dart';
import '../../domain/usecases/purchase_order_usecases.dart';

final purchaseOrdersProvider =
    AsyncNotifierProviderFamily<PurchaseOrdersNotifier, List<PurchaseOrder>, String>(
  PurchaseOrdersNotifier.new,
);

class PurchaseOrdersNotifier extends FamilyAsyncNotifier<List<PurchaseOrder>, String> {
  @override
  Future<List<PurchaseOrder>> build(String clientId) => _load(clientId);

  Future<List<PurchaseOrder>> _load(String clientId) async {
    final result = await sl<GetPurchaseOrdersByClientUseCase>()(ClientIdParams(clientId));
    return result.fold(
      (failure) => throw Exception(failure.message),
      (purchaseOrders) => purchaseOrders,
    );
  }

  Future<void> createPurchaseOrder(CreatePurchaseOrderParams params) async {
    final result = await sl<CreatePurchaseOrderUseCase>()(params);
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> updatePurchaseOrder(String id, UpdatePurchaseOrderParams params) async {
    final result = await sl<UpdatePurchaseOrderUseCase>()(
      UpdatePurchaseOrderRequest(purchaseOrderId: id, params: params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }
}

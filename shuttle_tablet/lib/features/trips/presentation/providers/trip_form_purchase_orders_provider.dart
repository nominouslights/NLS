import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../clients/domain/entities/purchase_order.dart';
import '../../../clients/domain/usecases/get_client_by_id_usecase.dart';
import '../../../clients/domain/usecases/purchase_order_usecases.dart';

/// PO list for trip form dropdown. Surfaces load failures so the UI can warn the user.
final tripFormPurchaseOrdersProvider =
    FutureProvider.family<List<PurchaseOrder>, String>((ref, clientId) async {
  final result = await sl<GetPurchaseOrdersByClientUseCase>()(ClientIdParams(clientId));
  return result.fold(
    (failure) => throw Exception(failure.message),
    (purchaseOrders) => purchaseOrders,
  );
});

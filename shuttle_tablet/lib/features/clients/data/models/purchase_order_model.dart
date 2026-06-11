import '../../../../core/utils/json_helpers.dart';
import '../../domain/entities/purchase_order.dart';
import 'purchase_order_line_item_model.dart';

class PurchaseOrderModel extends PurchaseOrder {
  const PurchaseOrderModel({
    required super.id,
    required super.clientId,
    required super.poNumber,
    required super.startDate,
    super.details,
    required super.totalValue,
    super.lineItemCount = 0,
    super.lineItems = const [],
    super.linkedContractIds = const [],
  });

  factory PurchaseOrderModel.fromSummaryJson(Map<String, dynamic> json) {
    return PurchaseOrderModel(
      id: jsonString(json, 'id'),
      clientId: jsonString(json, 'clientId'),
      poNumber: jsonString(json, 'poNumber'),
      startDate: jsonDateTime(json, 'startDate'),
      details: jsonStringOrNull(json, 'details'),
      totalValue: jsonDouble(json, 'totalValue'),
      lineItemCount: jsonInt(json, 'lineItemCount'),
      linkedContractIds: jsonStringList(json, 'linkedContractIds'),
    );
  }

  factory PurchaseOrderModel.fromDetailJson(Map<String, dynamic> json) {
    final lineItemsJson = jsonField(json, 'lineItems');
    final lineItems = lineItemsJson is List
        ? lineItemsJson
            .map(
              (e) => PurchaseOrderLineItemModel.fromJson(
                e as Map<String, dynamic>,
              ),
            )
            .toList()
        : <PurchaseOrderLineItemModel>[];

    return PurchaseOrderModel(
      id: jsonString(json, 'id'),
      clientId: jsonString(json, 'clientId'),
      poNumber: jsonString(json, 'poNumber'),
      startDate: jsonDateTime(json, 'startDate'),
      details: jsonStringOrNull(json, 'details'),
      totalValue: jsonDouble(json, 'totalValue'),
      lineItemCount: lineItems.length,
      lineItems: lineItems,
      linkedContractIds: jsonStringList(json, 'linkedContractIds'),
    );
  }
}

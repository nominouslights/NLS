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
    final linked = json['linkedContractIds'] as List<dynamic>? ?? [];
    return PurchaseOrderModel(
      id: json['id'] as String,
      clientId: json['clientId'] as String,
      poNumber: json['poNumber'] as String,
      startDate: DateTime.parse(json['startDate'] as String),
      details: json['details'] as String?,
      totalValue: (json['totalValue'] as num).toDouble(),
      lineItemCount: json['lineItemCount'] as int? ?? 0,
      linkedContractIds: linked.map((e) => e.toString()).toList(),
    );
  }

  factory PurchaseOrderModel.fromDetailJson(Map<String, dynamic> json) {
    final lineItemsJson = json['lineItems'] as List<dynamic>? ?? [];
    final linked = json['linkedContractIds'] as List<dynamic>? ?? [];
    return PurchaseOrderModel(
      id: json['id'] as String,
      clientId: json['clientId'] as String,
      poNumber: json['poNumber'] as String,
      startDate: DateTime.parse(json['startDate'] as String),
      details: json['details'] as String?,
      totalValue: (json['totalValue'] as num).toDouble(),
      lineItemCount: lineItemsJson.length,
      lineItems: lineItemsJson
          .map((e) => PurchaseOrderLineItemModel.fromJson(e as Map<String, dynamic>))
          .toList(),
      linkedContractIds: linked.map((e) => e.toString()).toList(),
    );
  }
}

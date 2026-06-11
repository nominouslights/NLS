import '../../domain/entities/purchase_order_line_item.dart';

class PurchaseOrderLineItemModel extends PurchaseOrderLineItem {
  const PurchaseOrderLineItemModel({
    required super.id,
    required super.description,
    required super.unitRate,
    required super.quantity,
    required super.lineTotal,
    super.sortOrder = 0,
  });

  factory PurchaseOrderLineItemModel.fromJson(Map<String, dynamic> json) {
    return PurchaseOrderLineItemModel(
      id: json['id'] as String,
      description: json['description'] as String,
      unitRate: (json['unitRate'] as num).toDouble(),
      quantity: (json['quantity'] as num).toDouble(),
      lineTotal: (json['lineTotal'] as num).toDouble(),
      sortOrder: json['sortOrder'] as int? ?? 0,
    );
  }

  Map<String, dynamic> toJson() => {
        'description': description,
        'unitRate': unitRate,
        'quantity': quantity,
      };
}

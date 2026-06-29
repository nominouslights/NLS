import '../../../../core/utils/json_helpers.dart';
import '../../domain/entities/invoice_line_item.dart';

class InvoiceLineItemModel extends InvoiceLineItem {
  const InvoiceLineItemModel({
    required super.id,
    super.tripId,
    required super.lineType,
    required super.description,
    required super.unitRate,
    required super.quantity,
    required super.lineTotal,
    required super.sortOrder,
  });

  factory InvoiceLineItemModel.fromJson(Map<String, dynamic> json) {
    return InvoiceLineItemModel(
      id: jsonString(json, 'id'),
      tripId: jsonStringOrNull(json, 'tripId'),
      lineType: jsonString(json, 'lineType'),
      description: jsonString(json, 'description'),
      unitRate: jsonDouble(json, 'unitRate'),
      quantity: jsonDouble(json, 'quantity'),
      lineTotal: jsonDouble(json, 'lineTotal'),
      sortOrder: jsonInt(json, 'sortOrder'),
    );
  }
}

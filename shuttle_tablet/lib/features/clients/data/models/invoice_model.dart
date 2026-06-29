import '../../../../core/utils/json_helpers.dart';
import '../../domain/entities/invoice.dart';
import 'invoice_line_item_model.dart';

class InvoiceModel extends Invoice {
  const InvoiceModel({
    required super.id,
    required super.invoiceNumber,
    required super.clientId,
    required super.clientName,
    required super.status,
    required super.issuedDate,
    required super.dueDate,
    super.notes,
    super.paidAt,
    required super.subTotal,
    required super.totalAmount,
    required super.lineItems,
  });

  /// Parses summary JSON (list endpoint) — no lineItems.
  factory InvoiceModel.fromSummaryJson(Map<String, dynamic> json) {
    return InvoiceModel(
      id: jsonString(json, 'id'),
      invoiceNumber: jsonString(json, 'invoiceNumber'),
      clientId: jsonString(json, 'clientId'),
      clientName: jsonString(json, 'clientName'),
      status: jsonString(json, 'status'),
      issuedDate: jsonDateTime(json, 'issuedDate'),
      dueDate: jsonDateTime(json, 'dueDate'),
      notes: jsonStringOrNull(json, 'notes'),
      paidAt: _parseDateTimeOrNull(json, 'paidAt'),
      subTotal: _parseOptionalDouble(json, 'subTotal'),
      totalAmount: jsonDouble(json, 'totalAmount'),
      lineItems: const [],
    );
  }

  /// Parses detail JSON (single invoice endpoint) — includes lineItems.
  factory InvoiceModel.fromDetailJson(Map<String, dynamic> json) {
    final rawLines = jsonField(json, 'lineItems');
    final lineItems = rawLines is List
        ? rawLines
            .map((e) => InvoiceLineItemModel.fromJson(e as Map<String, dynamic>))
            .toList()
        : <InvoiceLineItemModel>[];

    return InvoiceModel(
      id: jsonString(json, 'id'),
      invoiceNumber: jsonString(json, 'invoiceNumber'),
      clientId: jsonString(json, 'clientId'),
      clientName: jsonString(json, 'clientName'),
      status: jsonString(json, 'status'),
      issuedDate: jsonDateTime(json, 'issuedDate'),
      dueDate: jsonDateTime(json, 'dueDate'),
      notes: jsonStringOrNull(json, 'notes'),
      paidAt: _parseDateTimeOrNull(json, 'paidAt'),
      subTotal: _parseOptionalDouble(json, 'subTotal'),
      totalAmount: jsonDouble(json, 'totalAmount'),
      lineItems: lineItems,
    );
  }

  static DateTime? _parseDateTimeOrNull(Map<String, dynamic> json, String key) {
    final raw = jsonStringOrNull(json, key);
    if (raw == null) return null;
    return DateTime.tryParse(raw);
  }

  static double _parseOptionalDouble(Map<String, dynamic> json, String key) {
    final value = jsonField(json, key);
    if (value == null) return 0.0;
    if (value is num) return value.toDouble();
    return double.tryParse(value.toString()) ?? 0.0;
  }
}

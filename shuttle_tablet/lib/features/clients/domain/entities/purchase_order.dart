import 'package:equatable/equatable.dart';
import 'purchase_order_line_item.dart';

class PurchaseOrder extends Equatable {
  final String id;
  final String clientId;
  final String poNumber;
  final DateTime startDate;
  final String? details;
  final double totalValue;
  final int lineItemCount;
  final List<PurchaseOrderLineItem> lineItems;
  final List<String> linkedContractIds;

  const PurchaseOrder({
    required this.id,
    required this.clientId,
    required this.poNumber,
    required this.startDate,
    this.details,
    required this.totalValue,
    this.lineItemCount = 0,
    this.lineItems = const [],
    this.linkedContractIds = const [],
  });

  @override
  List<Object?> get props => [
        id,
        clientId,
        poNumber,
        startDate,
        details,
        totalValue,
        lineItemCount,
        lineItems,
        linkedContractIds,
      ];
}

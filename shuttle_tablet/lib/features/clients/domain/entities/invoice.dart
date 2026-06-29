import 'invoice_line_item.dart';

class Invoice {
  final String id;
  final String invoiceNumber;
  final String clientId;
  final String clientName;
  final String status; // Draft | Sent | Paid | Overdue | Void
  final DateTime issuedDate;
  final DateTime dueDate;
  final String? notes;
  final DateTime? paidAt;
  final double subTotal;
  final double totalAmount;
  final List<InvoiceLineItem> lineItems;

  const Invoice({
    required this.id,
    required this.invoiceNumber,
    required this.clientId,
    required this.clientName,
    required this.status,
    required this.issuedDate,
    required this.dueDate,
    this.notes,
    this.paidAt,
    required this.subTotal,
    required this.totalAmount,
    required this.lineItems,
  });
}

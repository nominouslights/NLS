class InvoiceLineItem {
  final String id;
  final String? tripId;
  final String lineType; // PassengerService | Cargo
  final String description;
  final double unitRate;
  final double quantity;
  final double lineTotal;
  final int sortOrder;

  const InvoiceLineItem({
    required this.id,
    this.tripId,
    required this.lineType,
    required this.description,
    required this.unitRate,
    required this.quantity,
    required this.lineTotal,
    required this.sortOrder,
  });
}

import 'package:equatable/equatable.dart';

class PurchaseOrderLineItem extends Equatable {
  final String id;
  final String description;
  final double unitRate;
  final double quantity;
  final double lineTotal;
  final int sortOrder;

  const PurchaseOrderLineItem({
    required this.id,
    required this.description,
    required this.unitRate,
    required this.quantity,
    required this.lineTotal,
    this.sortOrder = 0,
  });

  @override
  List<Object?> get props => [id, description, unitRate, quantity, lineTotal, sortOrder];
}

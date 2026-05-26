import 'package:equatable/equatable.dart';

class TripInspectionItem extends Equatable {
  final String id;
  final String preInspectionId;
  final String itemName;
  final bool passed;
  final String? notes;

  const TripInspectionItem({
    required this.id,
    required this.preInspectionId,
    required this.itemName,
    required this.passed,
    this.notes,
  });

  @override
  List<Object?> get props => [id, preInspectionId, itemName, passed, notes];
}

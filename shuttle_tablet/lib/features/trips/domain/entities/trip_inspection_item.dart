import 'package:equatable/equatable.dart';

enum InspectionCategory {
  exteriorMechanical,
  safetyEquipment,
  interiorComfort,
  communicationsNavigation,
}

class TripInspectionItem extends Equatable {
  final String id;
  final String preInspectionId;
  final String itemName;
  final InspectionCategory category;
  final bool passed;
  final String? notes;

  const TripInspectionItem({
    required this.id,
    required this.preInspectionId,
    required this.itemName,
    required this.category,
    required this.passed,
    this.notes,
  });

  @override
  List<Object?> get props =>
      [id, preInspectionId, itemName, category, passed, notes];
}

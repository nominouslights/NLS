import '../../domain/entities/trip_inspection_item.dart';

class TripInspectionItemModel extends TripInspectionItem {
  const TripInspectionItemModel({
    required super.id,
    required super.preInspectionId,
    required super.itemName,
    required super.category,
    required super.passed,
    super.notes,
  });

  factory TripInspectionItemModel.fromJson(Map<String, dynamic> json) =>
      TripInspectionItemModel(
        id: json['id'] as String,
        preInspectionId: json['preInspectionId'] as String? ?? '',
        itemName: json['itemName'] as String,
        category: _parseCategory(json['category'] as String?),
        passed: json['passed'] as bool,
        notes: json['notes'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'itemName': itemName,
        'category': _categoryToString(category),
        'passed': passed,
        'notes': notes,
      };

  static InspectionCategory _parseCategory(String? value) {
    switch (value) {
      case 'SafetyEquipment':
        return InspectionCategory.safetyEquipment;
      case 'InteriorComfort':
        return InspectionCategory.interiorComfort;
      case 'CommunicationsNavigation':
        return InspectionCategory.communicationsNavigation;
      default:
        return InspectionCategory.exteriorMechanical;
    }
  }

  static String _categoryToString(InspectionCategory cat) {
    switch (cat) {
      case InspectionCategory.exteriorMechanical:
        return 'ExteriorMechanical';
      case InspectionCategory.safetyEquipment:
        return 'SafetyEquipment';
      case InspectionCategory.interiorComfort:
        return 'InteriorComfort';
      case InspectionCategory.communicationsNavigation:
        return 'CommunicationsNavigation';
    }
  }
}

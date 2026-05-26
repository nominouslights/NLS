import '../../domain/entities/trip_inspection_item.dart';

class TripInspectionItemModel extends TripInspectionItem {
  const TripInspectionItemModel({
    required super.id,
    required super.preInspectionId,
    required super.itemName,
    required super.passed,
    super.notes,
  });

  factory TripInspectionItemModel.fromJson(Map<String, dynamic> json) =>
      TripInspectionItemModel(
        id: json['id'] as String,
        preInspectionId: json['preInspectionId'] as String? ?? '',
        itemName: json['itemName'] as String,
        passed: json['passed'] as bool,
        notes: json['notes'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'itemName': itemName,
        'passed': passed,
        'notes': notes,
      };
}

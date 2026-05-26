import '../../domain/entities/trip_pre_inspection.dart';
import 'trip_inspection_item_model.dart';

class TripPreInspectionModel extends TripPreInspection {
  const TripPreInspectionModel({
    required super.id,
    required super.tripId,
    required super.odometerStart,
    required super.submittedAt,
    super.items = const [],
  });

  factory TripPreInspectionModel.fromJson(Map<String, dynamic> json) {
    final itemsJson = json['items'] as List<dynamic>? ?? [];
    return TripPreInspectionModel(
      id: json['id'] as String,
      tripId: json['tripId'] as String? ?? '',
      odometerStart: json['odometerStart'] as int,
      submittedAt: DateTime.tryParse(json['submittedAt'] as String? ?? '') ??
          DateTime.now(),
      items: itemsJson
          .map((e) =>
              TripInspectionItemModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

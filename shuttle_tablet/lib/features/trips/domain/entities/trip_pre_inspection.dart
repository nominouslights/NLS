import 'package:equatable/equatable.dart';
import 'trip_inspection_item.dart';

class TripPreInspection extends Equatable {
  final String id;
  final String tripId;
  final int odometerStart;
  final DateTime submittedAt;
  final List<TripInspectionItem> items;

  const TripPreInspection({
    required this.id,
    required this.tripId,
    required this.odometerStart,
    required this.submittedAt,
    this.items = const [],
  });

  @override
  List<Object?> get props => [id, tripId, odometerStart, submittedAt, items];
}

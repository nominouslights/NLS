import 'package:equatable/equatable.dart';

class TripStop extends Equatable {
  final String id;
  final String tripId;
  final int sequenceOrder;
  final String locationName;
  final String? address;
  final DateTime? arrivedAt;
  final DateTime? departedAt;

  const TripStop({
    required this.id,
    required this.tripId,
    required this.sequenceOrder,
    required this.locationName,
    this.address,
    this.arrivedAt,
    this.departedAt,
  });

  @override
  List<Object?> get props =>
      [id, tripId, sequenceOrder, locationName, address, arrivedAt, departedAt];
}

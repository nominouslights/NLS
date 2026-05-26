import 'package:equatable/equatable.dart';

class TripStop extends Equatable {
  final String id;
  final String tripId;
  final int sequenceOrder;
  final String locationName;
  final String? address;

  const TripStop({
    required this.id,
    required this.tripId,
    required this.sequenceOrder,
    required this.locationName,
    this.address,
  });

  @override
  List<Object?> get props => [id, tripId, sequenceOrder, locationName, address];
}

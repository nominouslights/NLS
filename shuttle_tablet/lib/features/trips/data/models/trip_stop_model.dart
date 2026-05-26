import '../../domain/entities/trip_stop.dart';

class TripStopModel extends TripStop {
  const TripStopModel({
    required super.id,
    required super.tripId,
    required super.sequenceOrder,
    required super.locationName,
    super.address,
  });

  factory TripStopModel.fromJson(Map<String, dynamic> json) => TripStopModel(
        id: json['id'] as String,
        tripId: json['tripId'] as String? ?? '',
        sequenceOrder: json['sequenceOrder'] as int,
        locationName: json['locationName'] as String,
        address: json['address'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'sequenceOrder': sequenceOrder,
        'locationName': locationName,
        'address': address,
      };
}

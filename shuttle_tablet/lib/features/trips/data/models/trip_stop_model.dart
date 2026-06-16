import '../../domain/entities/trip_stop.dart';

class TripStopModel extends TripStop {
  const TripStopModel({
    required super.id,
    required super.tripId,
    required super.sequenceOrder,
    required super.locationName,
    super.address,
    super.arrivedAt,
    super.departedAt,
  });

  factory TripStopModel.fromJson(Map<String, dynamic> json) => TripStopModel(
        id: json['id'] as String,
        tripId: json['tripId'] as String? ?? '',
        sequenceOrder: json['sequenceOrder'] as int,
        locationName: json['locationName'] as String,
        address: json['address'] as String?,
        arrivedAt: json['arrivedAt'] != null
            ? DateTime.parse(json['arrivedAt'] as String)
            : null,
        departedAt: json['departedAt'] != null
            ? DateTime.parse(json['departedAt'] as String)
            : null,
      );

  Map<String, dynamic> toJson() => {
        'sequenceOrder': sequenceOrder,
        'locationName': locationName,
        'address': address,
      };
}

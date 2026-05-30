import '../../domain/entities/community_booking.dart';

class CommunityBookingModel extends CommunityBooking {
  const CommunityBookingModel({
    required super.bookingReference,
    required super.fullName,
    super.phone,
    super.email,
    super.direction,
    required super.tripType,
    required super.departureDate,
    required super.route,
    required super.fare,
    required super.status,
    super.cutoffDeadline,
    required super.bookedAt,
  });

  factory CommunityBookingModel.fromJson(Map<String, dynamic> json) {
    return CommunityBookingModel(
      bookingReference: json['bookingReference'] as String,
      fullName: json['fullName'] as String,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      direction: _parseDirection(json['direction'] as String?),
      tripType: _parseTripType(json['tripType'] as String? ?? ''),
      departureDate:
          DateTime.tryParse(json['departureDate'] as String? ?? '') ??
              DateTime.now(),
      route: json['route'] as String? ?? '',
      fare: (json['fare'] as num?)?.toDouble() ?? 0.0,
      status: json['status'] as String? ?? '',
      cutoffDeadline: json['cutoffDeadline'] != null
          ? DateTime.tryParse(json['cutoffDeadline'] as String)
          : null,
      bookedAt:
          DateTime.tryParse(json['bookedAt'] as String? ?? '') ??
              DateTime.now(),
    );
  }

  static TripDirection? _parseDirection(String? value) {
    return switch (value) {
      'Outbound' => TripDirection.outbound,
      'Inbound' => TripDirection.inbound,
      _ => null,
    };
  }

  static TripType _parseTripType(String value) {
    return value == 'Return' ? TripType.returnTrip : TripType.oneWay;
  }
}

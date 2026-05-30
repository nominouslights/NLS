import 'package:equatable/equatable.dart';

enum TripType { oneWay, returnTrip }

enum TripDirection { outbound, inbound }

class CommunityBooking extends Equatable {
  final String bookingReference;
  final String fullName;
  final String? phone;
  final String? email;
  final TripDirection? direction;
  final TripType tripType;
  final DateTime departureDate;
  final String route;
  final double fare;
  final String status;
  final DateTime? cutoffDeadline;
  final DateTime bookedAt;

  const CommunityBooking({
    required this.bookingReference,
    required this.fullName,
    this.phone,
    this.email,
    this.direction,
    required this.tripType,
    required this.departureDate,
    required this.route,
    required this.fare,
    required this.status,
    this.cutoffDeadline,
    required this.bookedAt,
  });

  @override
  List<Object?> get props => [
        bookingReference,
        fullName,
        phone,
        email,
        direction,
        tripType,
        departureDate,
        route,
        fare,
        status,
        cutoffDeadline,
        bookedAt,
      ];
}

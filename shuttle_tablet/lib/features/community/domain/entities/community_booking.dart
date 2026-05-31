import 'package:equatable/equatable.dart';

enum TripType { oneWay, returnTrip }

enum TripDirection { outbound, inbound }

enum TripDestination {
  lynnLake,
  leafRapids;

  String get displayName => switch (this) {
        TripDestination.lynnLake => 'Lynn Lake',
        TripDestination.leafRapids => 'Leaf Rapids',
      };

  String get apiValue => switch (this) {
        TripDestination.lynnLake => 'LynnLake',
        TripDestination.leafRapids => 'LeafRapids',
      };

  double get oneWayFare => switch (this) {
        TripDestination.lynnLake => 120.0,
        TripDestination.leafRapids => 100.0,
      };

  double get returnFare => oneWayFare * 2;
}

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

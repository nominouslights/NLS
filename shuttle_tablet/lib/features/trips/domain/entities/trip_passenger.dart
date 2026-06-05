import 'package:equatable/equatable.dart';

enum PassengerPaymentStatus {
  tentative,
  awaitingPayment,
  confirmed,
  released,
  cancelled,
  // Legacy API values mapped to tentative/confirmed on parse
  pending,
  paid,
}

class TripPassenger extends Equatable {
  final String id;
  final String tripId;
  final String name;
  final String? contactInfo;
  final int? seatNumber;
  final PassengerPaymentStatus paymentStatus;
  final String? bookingReference;
  final String? phone;
  final String? email;
  final String? direction;
  final DateTime? cutoffDeadline;
  final DateTime? bookedAt;
  final double? fare;
  final bool isAddedAfterDeparture;

  const TripPassenger({
    required this.id,
    required this.tripId,
    required this.name,
    this.contactInfo,
    this.seatNumber,
    required this.paymentStatus,
    this.bookingReference,
    this.phone,
    this.email,
    this.direction,
    this.cutoffDeadline,
    this.bookedAt,
    this.fare,
    this.isAddedAfterDeparture = false,
  });

  @override
  List<Object?> get props => [
        id,
        tripId,
        name,
        contactInfo,
        seatNumber,
        paymentStatus,
        bookingReference,
        phone,
        email,
        direction,
        cutoffDeadline,
        bookedAt,
        fare,
        isAddedAfterDeparture,
      ];
}

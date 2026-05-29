import 'package:equatable/equatable.dart';

enum PassengerPaymentStatus { pending, paid, cancelled }

class TripPassenger extends Equatable {
  final String id;
  final String tripId;
  final String name;
  final String? contactInfo;
  final int? seatNumber;
  final PassengerPaymentStatus paymentStatus;

  const TripPassenger({
    required this.id,
    required this.tripId,
    required this.name,
    this.contactInfo,
    this.seatNumber,
    required this.paymentStatus,
  });

  @override
  List<Object?> get props =>
      [id, tripId, name, contactInfo, seatNumber, paymentStatus];
}

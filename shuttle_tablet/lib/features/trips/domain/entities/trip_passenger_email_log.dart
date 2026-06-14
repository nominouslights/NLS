import 'package:equatable/equatable.dart';

class TripPassengerEmailLog extends Equatable {
  final String id;
  final String tripPassengerId;
  final String recipientEmail;
  final String direction;
  final DateTime sentAt;
  final bool isTest;

  const TripPassengerEmailLog({
    required this.id,
    required this.tripPassengerId,
    required this.recipientEmail,
    required this.direction,
    required this.sentAt,
    required this.isTest,
  });

  @override
  List<Object?> get props =>
      [id, tripPassengerId, recipientEmail, direction, sentAt, isTest];
}

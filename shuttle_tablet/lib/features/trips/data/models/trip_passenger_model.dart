import '../../domain/entities/trip_passenger.dart';

class TripPassengerModel extends TripPassenger {
  const TripPassengerModel({
    required super.id,
    required super.tripId,
    required super.name,
    super.contactInfo,
    super.seatNumber,
    required super.paymentStatus,
  });

  factory TripPassengerModel.fromJson(Map<String, dynamic> json) {
    return TripPassengerModel(
      id: json['id'] as String,
      tripId: json['tripId'] as String,
      name: json['name'] as String,
      contactInfo: json['contactInfo'] as String?,
      seatNumber: json['seatNumber'] as int?,
      paymentStatus: _parsePaymentStatus(json['paymentStatus'] as String? ?? ''),
    );
  }

  static PassengerPaymentStatus _parsePaymentStatus(String value) {
    const map = {
      'Pending': PassengerPaymentStatus.pending,
      'Paid': PassengerPaymentStatus.paid,
      'Cancelled': PassengerPaymentStatus.cancelled,
    };
    return map[value] ?? PassengerPaymentStatus.pending;
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'tripId': tripId,
        'name': name,
        'contactInfo': contactInfo,
        'seatNumber': seatNumber,
        'paymentStatus': _paymentStatusToString(paymentStatus),
      };

  static String _paymentStatusToString(PassengerPaymentStatus status) {
    const map = {
      PassengerPaymentStatus.pending: 'Pending',
      PassengerPaymentStatus.paid: 'Paid',
      PassengerPaymentStatus.cancelled: 'Cancelled',
    };
    return map[status]!;
  }
}

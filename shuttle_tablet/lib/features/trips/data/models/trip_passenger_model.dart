import '../../domain/entities/trip_passenger.dart';
import 'trip_passenger_email_log_model.dart';

class TripPassengerModel extends TripPassenger {
  const TripPassengerModel({
    required super.id,
    required super.tripId,
    required super.name,
    super.contactInfo,
    super.seatNumber,
    required super.paymentStatus,
    super.boardingStatus = PassengerBoardingStatus.notBoarded,
    super.bookingReference,
    super.phone,
    super.email,
    super.direction,
    super.cutoffDeadline,
    super.bookedAt,
    super.fare,
    super.isAddedAfterDeparture = false,
    super.emailLogs = const [],
  });

  factory TripPassengerModel.fromJson(Map<String, dynamic> json) {
    return TripPassengerModel(
      id: json['id'] as String,
      tripId: json['tripId'] as String,
      name: json['name'] as String,
      contactInfo: json['contactInfo'] as String?,
      seatNumber: json['seatNumber'] as int?,
      paymentStatus: _parsePaymentStatus(json['paymentStatus'] as String? ?? ''),
      boardingStatus: _parseBoardingStatus(json['boardingStatus'] as String?),
      bookingReference: json['bookingReference'] as String?,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      direction: json['direction'] as String?,
      cutoffDeadline: json['cutoffDeadline'] != null
          ? DateTime.tryParse(json['cutoffDeadline'] as String)
          : null,
      bookedAt: json['bookedAt'] != null
          ? DateTime.tryParse(json['bookedAt'] as String)
          : null,
      fare: (json['fare'] as num?)?.toDouble(),
      isAddedAfterDeparture: json['isAddedAfterDeparture'] as bool? ?? false,
      emailLogs: (json['emailLogs'] as List<dynamic>? ?? [])
          .map((e) => TripPassengerEmailLogModel.fromJson(e as Map<String, dynamic>))
          .map((m) => m.toEntity())
          .toList(),
    );
  }

  static PassengerBoardingStatus _parseBoardingStatus(String? value) {
    switch (value) {
      case 'Boarded':
        return PassengerBoardingStatus.boarded;
      case 'NoShow':
        return PassengerBoardingStatus.noShow;
      default:
        return PassengerBoardingStatus.notBoarded;
    }
  }

  static PassengerPaymentStatus _parsePaymentStatus(String value) {
    const map = {
      'Tentative': PassengerPaymentStatus.tentative,
      'AwaitingPayment': PassengerPaymentStatus.awaitingPayment,
      'Confirmed': PassengerPaymentStatus.confirmed,
      'Released': PassengerPaymentStatus.released,
      'Cancelled': PassengerPaymentStatus.cancelled,
      // Backward compat
      'Pending': PassengerPaymentStatus.tentative,
      'Paid': PassengerPaymentStatus.confirmed,
    };
    return map[value] ?? PassengerPaymentStatus.tentative;
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'tripId': tripId,
        'name': name,
        'contactInfo': contactInfo,
        'seatNumber': seatNumber,
        'paymentStatus': _paymentStatusToString(paymentStatus),
        'bookingReference': bookingReference,
        'phone': phone,
        'email': email,
        'direction': direction,
        'cutoffDeadline': cutoffDeadline?.toIso8601String(),
        'bookedAt': bookedAt?.toIso8601String(),
        'fare': fare,
        'isAddedAfterDeparture': isAddedAfterDeparture,
      };

  static String _paymentStatusToString(PassengerPaymentStatus status) {
    const map = {
      PassengerPaymentStatus.tentative: 'Tentative',
      PassengerPaymentStatus.awaitingPayment: 'AwaitingPayment',
      PassengerPaymentStatus.confirmed: 'Confirmed',
      PassengerPaymentStatus.released: 'Released',
      PassengerPaymentStatus.cancelled: 'Cancelled',
      PassengerPaymentStatus.pending: 'Tentative',
      PassengerPaymentStatus.paid: 'Confirmed',
    };
    return map[status]!;
  }
}

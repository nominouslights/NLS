import 'package:equatable/equatable.dart';

enum CalendarDayStatus { go, building, open, unavailable }

class CalendarDay extends Equatable {
  final DateTime date;
  final String dayOfWeek;
  final CalendarDayStatus status;
  final bool isZone2;
  final int confirmedCount;
  final int tentativeCount;
  final int availableSeats;
  final String? tripId;
  final bool isBlocked;
  final String? blockReason;

  const CalendarDay({
    required this.date,
    required this.dayOfWeek,
    required this.status,
    required this.isZone2,
    required this.confirmedCount,
    required this.tentativeCount,
    required this.availableSeats,
    this.tripId,
    required this.isBlocked,
    this.blockReason,
  });

  @override
  List<Object?> get props => [
        date,
        dayOfWeek,
        status,
        isZone2,
        confirmedCount,
        tentativeCount,
        availableSeats,
        tripId,
        isBlocked,
        blockReason,
      ];
}

import '../../domain/entities/calendar_day.dart';

class CalendarDayModel extends CalendarDay {
  const CalendarDayModel({
    required super.date,
    required super.dayOfWeek,
    required super.status,
    required super.isZone2,
    required super.confirmedCount,
    required super.tentativeCount,
    required super.availableSeats,
    super.tripId,
    required super.isBlocked,
    super.blockReason,
  });

  factory CalendarDayModel.fromJson(Map<String, dynamic> json) {
    return CalendarDayModel(
      date: DateTime.tryParse(json['date'] as String? ?? '') ?? DateTime.now(),
      dayOfWeek: json['dayOfWeek'] as String? ?? '',
      status: _parseStatus(json['status'] as String? ?? ''),
      isZone2: json['isZone2'] as bool? ?? false,
      confirmedCount: json['confirmedCount'] as int? ?? 0,
      tentativeCount: json['tentativeCount'] as int? ?? 0,
      availableSeats: json['availableSeats'] as int? ?? 0,
      tripId: json['tripId'] as String?,
      isBlocked: json['isBlocked'] as bool? ?? false,
      blockReason: json['blockReason'] as String?,
    );
  }

  static CalendarDayStatus _parseStatus(String value) {
    return switch (value) {
      'Go' => CalendarDayStatus.go,
      'Building' => CalendarDayStatus.building,
      'Open' => CalendarDayStatus.open,
      _ => CalendarDayStatus.unavailable,
    };
  }
}

import '../../domain/entities/driver_roster_entry.dart';

class DriverRosterEntryModel extends DriverRosterEntry {
  const DriverRosterEntryModel({
    required super.id,
    required super.driverId,
    required super.entryDate,
    required super.status,
    super.shiftStart,
    super.shiftEnd,
  });

  factory DriverRosterEntryModel.fromJson(Map<String, dynamic> json) {
    return DriverRosterEntryModel(
      id: json['id'] as String,
      driverId: json['driverId'] as String,
      // API returns DateOnly as "yyyy-MM-dd" — parse as UTC midnight so no TZ shift
      entryDate: DateTime.parse('${json['entryDate']}T00:00:00Z'),
      status: _parseRosterStatus(json['status'] as String? ?? ''),
      // API returns TimeOnly as "HH:mm:ss" — trim to "HH:mm"
      shiftStart: _trimTime(json['shiftStart'] as String?),
      shiftEnd: _trimTime(json['shiftEnd'] as String?),
    );
  }

  static RosterStatus _parseRosterStatus(String value) {
    return switch (value.toLowerCase()) {
      'unavailable' => RosterStatus.unavailable,
      'scheduled' => RosterStatus.scheduled,
      'ontrip' => RosterStatus.onTrip,
      _ => RosterStatus.available,
    };
  }

  static String? _trimTime(String? value) {
    if (value == null) return null;
    // "HH:mm:ss" → "HH:mm"
    if (value.length > 5) return value.substring(0, 5);
    return value;
  }
}

/// A model that carries a driver's roster summary (used by the fleet view).
class DriverRosterSummaryModel {
  final String driverId;
  final String employeeId;
  final String fullName;
  final List<DriverRosterEntryModel> entries;

  const DriverRosterSummaryModel({
    required this.driverId,
    required this.employeeId,
    required this.fullName,
    required this.entries,
  });

  factory DriverRosterSummaryModel.fromJson(Map<String, dynamic> json) {
    final entriesJson = json['entries'] as List<dynamic>? ?? [];
    return DriverRosterSummaryModel(
      driverId: json['driverId'] as String,
      employeeId: json['employeeId'] as String,
      fullName: json['fullName'] as String,
      entries: entriesJson
          .map((e) => DriverRosterEntryModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

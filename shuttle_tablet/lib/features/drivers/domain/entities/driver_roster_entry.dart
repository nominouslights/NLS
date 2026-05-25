import 'package:equatable/equatable.dart';

enum RosterStatus { available, unavailable, scheduled, onTrip }

class DriverRosterEntry extends Equatable {
  final String id;
  final String driverId;
  final DateTime entryDate;
  final RosterStatus status;
  final String? shiftStart; // "HH:mm" format
  final String? shiftEnd;   // "HH:mm" format

  const DriverRosterEntry({
    required this.id,
    required this.driverId,
    required this.entryDate,
    required this.status,
    this.shiftStart,
    this.shiftEnd,
  });

  @override
  List<Object?> get props => [id, driverId, entryDate, status, shiftStart, shiftEnd];
}

import 'package:equatable/equatable.dart';

class VehicleOdometerEntry extends Equatable {
  final DateTime date;
  final int odometerKm;
  final String source; // "Trip" | "Service" | "Fuel"
  final String referenceId;
  final String? notes;

  const VehicleOdometerEntry({
    required this.date,
    required this.odometerKm,
    required this.source,
    required this.referenceId,
    this.notes,
  });

  @override
  List<Object?> get props =>
      [date, odometerKm, source, referenceId, notes];
}

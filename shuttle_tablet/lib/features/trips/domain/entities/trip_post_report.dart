import 'package:equatable/equatable.dart';

enum IncidentType { delay, passengerNoShow, vehicleIssue, cargoDamage, accident }

class TripPostReport extends Equatable {
  final String id;
  final String tripId;
  final int odometerStart;
  final int odometerEnd;
  final int distanceKm;
  final double? fuelAddedLitres;
  final double? fuelCostDollars;
  final bool hasIncident;
  final IncidentType? incidentType;
  final String? incidentDescription;
  final String? additionalNotes;
  final DateTime submittedAt;
  final bool isReadyToInvoice;

  const TripPostReport({
    required this.id,
    required this.tripId,
    required this.odometerStart,
    required this.odometerEnd,
    required this.distanceKm,
    this.fuelAddedLitres,
    this.fuelCostDollars,
    required this.hasIncident,
    this.incidentType,
    this.incidentDescription,
    this.additionalNotes,
    required this.submittedAt,
    required this.isReadyToInvoice,
  });

  @override
  List<Object?> get props => [
        id,
        tripId,
        odometerStart,
        odometerEnd,
        distanceKm,
        fuelAddedLitres,
        fuelCostDollars,
        hasIncident,
        incidentType,
        incidentDescription,
        additionalNotes,
        submittedAt,
        isReadyToInvoice,
      ];
}

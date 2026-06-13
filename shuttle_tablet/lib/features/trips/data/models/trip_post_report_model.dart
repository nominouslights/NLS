import '../../domain/entities/trip_post_report.dart';

class TripPostReportModel extends TripPostReport {
  const TripPostReportModel({
    required super.id,
    required super.tripId,
    required super.odometerStart,
    required super.odometerEnd,
    required super.distanceKm,
    super.fuelAddedLitres,
    super.fuelCostDollars,
    required super.hasIncident,
    super.incidentType,
    super.incidentDescription,
    super.additionalNotes,
    required super.submittedAt,
    required super.isReadyToInvoice,
    super.exteriorNoNewDamage = false,
    super.interiorCleanedAndChecked = false,
    super.passengersDisembarkedSafely = false,
    super.allCargoDeliveredAndAccounted = false,
    super.vehicleSecuredAndPluggedIn = false,
    super.keysReturnedAndSecured = false,
  });

  factory TripPostReportModel.fromJson(Map<String, dynamic> json) =>
      TripPostReportModel(
        id: json['id'] as String,
        tripId: json['tripId'] as String? ?? '',
        odometerStart: json['odometerStart'] as int,
        odometerEnd: json['odometerEnd'] as int,
        distanceKm: json['distanceKm'] as int? ?? 0,
        fuelAddedLitres: (json['fuelAddedLitres'] as num?)?.toDouble(),
        fuelCostDollars: (json['fuelCostDollars'] as num?)?.toDouble(),
        hasIncident: json['hasIncident'] as bool? ?? false,
        incidentType: _parseIncidentType(json['incidentType'] as String?),
        incidentDescription: json['incidentDescription'] as String?,
        additionalNotes: json['additionalNotes'] as String?,
        submittedAt: DateTime.tryParse(json['submittedAt'] as String? ?? '') ??
            DateTime.now(),
        isReadyToInvoice: json['isReadyToInvoice'] as bool? ?? false,
        exteriorNoNewDamage: json['exteriorNoNewDamage'] as bool? ?? false,
        interiorCleanedAndChecked:
            json['interiorCleanedAndChecked'] as bool? ?? false,
        passengersDisembarkedSafely:
            json['passengersDisembarkedSafely'] as bool? ?? false,
        allCargoDeliveredAndAccounted:
            json['allCargoDeliveredAndAccounted'] as bool? ?? false,
        vehicleSecuredAndPluggedIn:
            json['vehicleSecuredAndPluggedIn'] as bool? ?? false,
        keysReturnedAndSecured:
            json['keysReturnedAndSecured'] as bool? ?? false,
      );

  static IncidentType? _parseIncidentType(String? value) {
    if (value == null) return null;
    const map = {
      'Delay': IncidentType.delay,
      'PassengerNoShow': IncidentType.passengerNoShow,
      'VehicleIssue': IncidentType.vehicleIssue,
      'CargoDamage': IncidentType.cargoDamage,
      'Accident': IncidentType.accident,
    };
    return map[value];
  }

  static String? _incidentTypeToString(IncidentType? type) {
    if (type == null) return null;
    const map = {
      IncidentType.delay: 'Delay',
      IncidentType.passengerNoShow: 'PassengerNoShow',
      IncidentType.vehicleIssue: 'VehicleIssue',
      IncidentType.cargoDamage: 'CargoDamage',
      IncidentType.accident: 'Accident',
    };
    return map[type];
  }

  Map<String, dynamic> toJson() => {
        'odometerEnd': odometerEnd,
        'fuelAddedLitres': fuelAddedLitres,
        'fuelCostDollars': fuelCostDollars,
        'hasIncident': hasIncident,
        'incidentType': _incidentTypeToString(incidentType),
        'incidentDescription': incidentDescription,
        'additionalNotes': additionalNotes,
        'isReadyToInvoice': isReadyToInvoice,
        'exteriorNoNewDamage': exteriorNoNewDamage,
        'interiorCleanedAndChecked': interiorCleanedAndChecked,
        'passengersDisembarkedSafely': passengersDisembarkedSafely,
        'allCargoDeliveredAndAccounted': allCargoDeliveredAndAccounted,
        'vehicleSecuredAndPluggedIn': vehicleSecuredAndPluggedIn,
        'keysReturnedAndSecured': keysReturnedAndSecured,
      };
}

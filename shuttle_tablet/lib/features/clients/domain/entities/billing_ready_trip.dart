class BillingReadyTrip {
  final String tripId;
  final DateTime scheduledAt;
  final String? direction;
  final List<String> passengerNames;
  final double totalCargoWeightKg;
  final String cargoSummary;

  const BillingReadyTrip({
    required this.tripId,
    required this.scheduledAt,
    this.direction,
    required this.passengerNames,
    required this.totalCargoWeightKg,
    required this.cargoSummary,
  });
}

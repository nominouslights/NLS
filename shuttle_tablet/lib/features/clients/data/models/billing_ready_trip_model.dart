import '../../../../core/utils/json_helpers.dart';
import '../../domain/entities/billing_ready_trip.dart';

class BillingReadyTripModel extends BillingReadyTrip {
  const BillingReadyTripModel({
    required super.tripId,
    required super.scheduledAt,
    super.direction,
    required super.passengerNames,
    required super.totalCargoWeightKg,
    required super.cargoSummary,
  });

  factory BillingReadyTripModel.fromJson(Map<String, dynamic> json) {
    final rawNames = jsonField(json, 'passengerNames');
    final passengerNames = rawNames is List
        ? rawNames.map((e) => e.toString()).toList()
        : <String>[];

    return BillingReadyTripModel(
      tripId: jsonString(json, 'tripId'),
      scheduledAt: jsonDateTime(json, 'scheduledAt'),
      direction: jsonStringOrNull(json, 'direction'),
      passengerNames: passengerNames,
      totalCargoWeightKg: _parseOptionalDouble(json, 'totalCargoWeightKg'),
      cargoSummary: jsonStringOrNull(json, 'cargoSummary') ?? '',
    );
  }

  static double _parseOptionalDouble(Map<String, dynamic> json, String key) {
    final value = jsonField(json, key);
    if (value == null) return 0.0;
    if (value is num) return value.toDouble();
    return double.tryParse(value.toString()) ?? 0.0;
  }
}

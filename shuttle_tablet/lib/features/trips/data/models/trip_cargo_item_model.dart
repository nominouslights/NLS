import '../../domain/entities/trip_cargo_item.dart';

class TripCargoItemModel extends TripCargoItem {
  const TripCargoItemModel({
    required super.id,
    required super.tripId,
    required super.cargoType,
    super.description,
    required super.quantity,
    super.weightKg,
    super.charge,
    super.isHazmat = false,
    super.isSecured = false,
  });

  factory TripCargoItemModel.fromJson(Map<String, dynamic> json) {
    return TripCargoItemModel(
      id: json['id'] as String,
      tripId: json['tripId'] as String,
      cargoType: _parseCargoType(json['cargoType'] as String? ?? ''),
      description: json['description'] as String?,
      quantity: json['quantity'] as int? ?? 1,
      weightKg: (json['weightKg'] as num?)?.toDouble(),
      charge: (json['charge'] as num?)?.toDouble(),
      isHazmat: json['isHazmat'] as bool? ?? false,
      isSecured: json['isSecured'] as bool? ?? false,
    );
  }

  static TripCargoType _parseCargoType(String value) {
    const map = {
      'Box': TripCargoType.box,
      'Pallet': TripCargoType.pallet,
    };
    return map[value] ?? TripCargoType.box;
  }

  static String _cargoTypeToString(TripCargoType type) {
    const map = {
      TripCargoType.box: 'Box',
      TripCargoType.pallet: 'Pallet',
    };
    return map[type]!;
  }

  Map<String, dynamic> toJson() => {
        'cargoType': _cargoTypeToString(cargoType),
        'description': description,
        'quantity': quantity,
        'weightKg': weightKg,
        'charge': charge,
        'isHazmat': isHazmat,
        'isSecured': isSecured,
      };
}

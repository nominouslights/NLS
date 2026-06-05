import '../../domain/entities/trip_cargo_item.dart';

class TripCargoItemModel extends TripCargoItem {
  const TripCargoItemModel({
    required super.id,
    required super.tripId,
    required super.cargoType,
    super.description,
    required super.quantity,
  });

  factory TripCargoItemModel.fromJson(Map<String, dynamic> json) {
    return TripCargoItemModel(
      id: json['id'] as String,
      tripId: json['tripId'] as String,
      cargoType: _parseCargoType(json['cargoType'] as String? ?? ''),
      description: json['description'] as String?,
      quantity: json['quantity'] as int? ?? 1,
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
      };
}

import 'package:equatable/equatable.dart';

enum TripCargoType { box, pallet }

class TripCargoItem extends Equatable {
  final String id;
  final String tripId;
  final TripCargoType cargoType;
  final String? description;
  final int quantity;
  final double? weightKg;
  final double? charge;
  final bool isHazmat;
  final bool isSecured;

  const TripCargoItem({
    required this.id,
    required this.tripId,
    required this.cargoType,
    this.description,
    required this.quantity,
    this.weightKg,
    this.charge,
    this.isHazmat = false,
    this.isSecured = false,
  });

  String get typeLabel => switch (cargoType) {
        TripCargoType.box => 'Box',
        TripCargoType.pallet => 'Pallet',
      };

  @override
  List<Object?> get props =>
      [id, tripId, cargoType, description, quantity, weightKg, charge, isHazmat, isSecured];
}

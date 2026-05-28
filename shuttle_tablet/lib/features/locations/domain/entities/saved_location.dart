import 'package:equatable/equatable.dart';

class SavedLocation extends Equatable {
  final String id;
  final String name;
  final String? address;
  final double? latitude;
  final double? longitude;
  final DateTime createdAt;

  const SavedLocation({
    required this.id,
    required this.name,
    this.address,
    this.latitude,
    this.longitude,
    required this.createdAt,
  });

  bool get hasCoordinates => latitude != null && longitude != null;

  @override
  List<Object?> get props => [id, name, address, latitude, longitude, createdAt];
}

import '../../domain/entities/saved_location.dart';

class SavedLocationModel extends SavedLocation {
  const SavedLocationModel({
    required super.id,
    required super.name,
    super.address,
    super.latitude,
    super.longitude,
    required super.createdAt,
  });

  factory SavedLocationModel.fromJson(Map<String, dynamic> json) {
    return SavedLocationModel(
      id: json['id'] as String,
      name: json['name'] as String,
      address: json['address'] as String?,
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
      createdAt: DateTime.tryParse(json['createdAt'] as String? ?? '') ?? DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'name': name,
        'address': address,
        'latitude': latitude,
        'longitude': longitude,
        'createdAt': createdAt.toIso8601String(),
      };
}

import '../../domain/entities/passenger_profile.dart';

class PassengerProfileModel extends PassengerProfile {
  const PassengerProfileModel({
    required super.id,
    required super.clientId,
    required super.name,
    super.phone,
    super.email,
    required super.lastBookedAt,
  });

  factory PassengerProfileModel.fromJson(
      Map<String, dynamic> json, String clientId) {
    return PassengerProfileModel(
      id: json['id'] as String,
      clientId: clientId,
      name: json['name'] as String,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      lastBookedAt:
          DateTime.parse(json['lastBookedAt'] as String),
    );
  }
}

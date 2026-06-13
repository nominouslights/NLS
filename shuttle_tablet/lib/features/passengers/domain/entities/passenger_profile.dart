import 'package:equatable/equatable.dart';

class PassengerProfile extends Equatable {
  final String id;
  final String clientId;
  final String name;
  final String? phone;
  final String? email;
  final DateTime lastBookedAt;

  const PassengerProfile({
    required this.id,
    required this.clientId,
    required this.name,
    this.phone,
    this.email,
    required this.lastBookedAt,
  });

  @override
  List<Object?> get props => [id, clientId, name, phone, email, lastBookedAt];
}

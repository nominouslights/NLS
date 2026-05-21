import 'package:equatable/equatable.dart';

class PendingUser extends Equatable {
  final String id;
  final String email;
  final String role;
  final DateTime createdAt;

  const PendingUser({
    required this.id,
    required this.email,
    required this.role,
    required this.createdAt,
  });

  @override
  List<Object?> get props => [id, email, role, createdAt];
}

import '../../domain/entities/pending_user.dart';

class PendingUserModel extends PendingUser {
  const PendingUserModel({
    required super.id,
    required super.email,
    required super.role,
    required super.createdAt,
  });

  factory PendingUserModel.fromJson(Map<String, dynamic> json) => PendingUserModel(
        id: json['id'] as String,
        email: json['email'] as String,
        role: json['role'] as String,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

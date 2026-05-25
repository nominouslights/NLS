import '../../domain/entities/driver.dart';
import 'driver_document_model.dart';

class DriverModel extends Driver {
  const DriverModel({
    required super.id,
    required super.employeeId,
    required super.firstName,
    required super.lastName,
    required super.phone,
    required super.email,
    required super.hireDate,
    required super.status,
    required super.isActive,
    required super.createdAt,
    super.hasExpiringDocuments = false,
    super.documents = const [],
  });

  factory DriverModel.fromJson(Map<String, dynamic> json) {
    final docsJson = json['documents'] as List<dynamic>?;
    return DriverModel(
      id: json['id'] as String,
      employeeId: json['employeeId'] as String,
      firstName: json['firstName'] as String,
      lastName: json['lastName'] as String,
      phone: json['phone'] as String,
      email: json['email'] as String,
      hireDate: DateTime.tryParse(json['hireDate'] as String? ?? '') ?? DateTime.now(),
      status: _parseDriverStatus(json['status'] as String? ?? ''),
      isActive: json['isActive'] as bool? ?? true,
      createdAt: DateTime.tryParse(json['createdAt'] as String? ?? '') ?? DateTime.now(),
      hasExpiringDocuments: json['hasExpiringDocuments'] as bool? ?? false,
      documents: docsJson != null
          ? docsJson
              .map((e) => DriverDocumentModel.fromJson(e as Map<String, dynamic>))
              .toList()
          : const [],
    );
  }

  Map<String, dynamic> toJson() => {
        'employeeId': employeeId,
        'firstName': firstName,
        'lastName': lastName,
        'phone': phone,
        'email': email,
        'hireDate': hireDate.toIso8601String(),
        'isActive': isActive,
      };

  static DriverStatus _parseDriverStatus(String value) {
    return switch (value.toLowerCase()) {
      'ontrip' => DriverStatus.onTrip,
      'offduty' => DriverStatus.offDuty,
      _ => DriverStatus.available,
    };
  }
}

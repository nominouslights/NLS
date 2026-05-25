import 'package:equatable/equatable.dart';
import 'driver_document.dart';

enum DriverStatus { available, onTrip, offDuty }

class Driver extends Equatable {
  final String id;
  final String employeeId;
  final String firstName;
  final String lastName;
  final String phone;
  final String email;
  final DateTime hireDate;
  final DriverStatus status;
  final bool isActive;
  final DateTime createdAt;
  final bool hasExpiringDocuments;
  final List<DriverDocument> documents;

  String get fullName => '$firstName $lastName';

  const Driver({
    required this.id,
    required this.employeeId,
    required this.firstName,
    required this.lastName,
    required this.phone,
    required this.email,
    required this.hireDate,
    required this.status,
    required this.isActive,
    required this.createdAt,
    this.hasExpiringDocuments = false,
    this.documents = const [],
  });

  @override
  List<Object?> get props => [
        id,
        employeeId,
        firstName,
        lastName,
        phone,
        email,
        hireDate,
        status,
        isActive,
        createdAt,
        hasExpiringDocuments,
        documents,
      ];
}

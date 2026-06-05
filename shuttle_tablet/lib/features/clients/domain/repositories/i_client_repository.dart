import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/client.dart';

abstract interface class IClientRepository {
  Future<Either<Failure, List<Client>>> getClients();
  Future<Either<Failure, Client>> getClientById(String id);
  Future<Either<Failure, String>> createClient(CreateClientParams params);
  Future<Either<Failure, void>> updateClient(String id, UpdateClientParams params);
  Future<Either<Failure, void>> deleteClient(String id);
}

class CreateClientParams {
  final String businessName;
  final ServiceType serviceType;
  final String primaryContactName;
  final String primaryContactTitle;
  final String phone;
  final String email;
  final String streetAddress;
  final String city;
  final String province;
  final String postalCode;
  final String? gstHstNumber;
  final String preferredPaymentMethod;
  final int netPaymentTerms;
  final String? complianceNotes;
  final bool isMinesite;
  final String? industry;
  final String? projectSite;

  const CreateClientParams({
    required this.businessName,
    required this.serviceType,
    required this.primaryContactName,
    required this.primaryContactTitle,
    required this.phone,
    required this.email,
    required this.streetAddress,
    required this.city,
    required this.province,
    required this.postalCode,
    this.gstHstNumber,
    required this.preferredPaymentMethod,
    required this.netPaymentTerms,
    this.complianceNotes,
    required this.isMinesite,
    this.industry,
    this.projectSite,
  });
}

class UpdateClientParams extends CreateClientParams {
  final bool isActive;
  final List<String>? notificationEmails;
  final List<String>? tripDepartureArrivalEmails;
  final List<String>? passengerBookingEmails;

  const UpdateClientParams({
    required super.businessName,
    required super.serviceType,
    required super.primaryContactName,
    required super.primaryContactTitle,
    required super.phone,
    required super.email,
    required super.streetAddress,
    required super.city,
    required super.province,
    required super.postalCode,
    super.gstHstNumber,
    required super.preferredPaymentMethod,
    required super.netPaymentTerms,
    super.complianceNotes,
    required super.isMinesite,
    required this.isActive,
    super.industry,
    super.projectSite,
    this.notificationEmails,
    this.tripDepartureArrivalEmails,
    this.passengerBookingEmails,
  });
}

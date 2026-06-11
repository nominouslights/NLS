import 'package:equatable/equatable.dart';
import 'contract.dart';

enum ServiceType { corporate, community }

class Client extends Equatable {
  final String id;
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
  final double outstandingBalance;
  final String? complianceNotes;
  final bool isMinesite;
  final bool isActive;
  final DateTime createdAt;
  final Contract? activeContract;
  // Populated from list endpoint only (no full contract object in list response)
  final DateTime? activeContractEndDate;
  final bool listItemIsExpiringSoon;
  final String? industry;
  final String? projectSite;
  final List<String> notificationEmails;
  final List<String> tripDepartureArrivalEmails;
  final List<String> passengerBookingEmails;
  /// True when GET /api/clients/{id} includes notification email fields.
  final bool apiSupportsNotificationEmails;

  const Client({
    required this.id,
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
    required this.outstandingBalance,
    this.complianceNotes,
    required this.isMinesite,
    required this.isActive,
    required this.createdAt,
    this.activeContract,
    this.activeContractEndDate,
    this.listItemIsExpiringSoon = false,
    this.industry,
    this.projectSite,
    this.notificationEmails = const [],
    this.tripDepartureArrivalEmails = const [],
    this.passengerBookingEmails = const [],
    this.apiSupportsNotificationEmails = false,
  });

  @override
  List<Object?> get props => [id, businessName, serviceType, primaryContactName, primaryContactTitle,
    phone, email, streetAddress, city, province, postalCode, gstHstNumber, preferredPaymentMethod,
    netPaymentTerms, outstandingBalance, complianceNotes, isMinesite, isActive, createdAt,
    activeContract, activeContractEndDate, listItemIsExpiringSoon, industry, projectSite,
    notificationEmails, tripDepartureArrivalEmails, passengerBookingEmails,
    apiSupportsNotificationEmails];
}

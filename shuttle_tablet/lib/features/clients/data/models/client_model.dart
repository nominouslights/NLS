import '../../domain/entities/client.dart';
import 'contract_model.dart';

class ClientModel extends Client {
  const ClientModel({
    required super.id,
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
    required super.outstandingBalance,
    super.complianceNotes,
    required super.isMinesite,
    required super.isActive,
    required super.createdAt,
    ContractModel? activeContract,
    super.activeContractRenewalDate,
    super.listItemIsExpiringSoon = false,
    super.industry,
    super.projectSite,
    super.notificationEmails = const [],
    super.tripDepartureArrivalEmails = const [],
    super.passengerBookingEmails = const [],
  }) : super(activeContract: activeContract);

  factory ClientModel.fromJson(Map<String, dynamic> json) {
    final contractJson = json['activeContract'] as Map<String, dynamic>?;
    final renewalDateRaw = json['activeContractRenewalDate'] as String?;
    return ClientModel(
      id: json['id'] as String,
      businessName: json['businessName'] as String,
      serviceType: _parseServiceType(json['serviceType'] as String),
      primaryContactName: json['primaryContactName'] as String,
      primaryContactTitle: json['primaryContactTitle'] as String? ?? '',
      phone: json['phone'] as String,
      email: json['email'] as String,
      streetAddress: json['streetAddress'] as String? ?? '',
      city: json['city'] as String? ?? '',
      province: json['province'] as String? ?? '',
      postalCode: json['postalCode'] as String? ?? '',
      gstHstNumber: json['gstHstNumber'] as String?,
      preferredPaymentMethod: json['preferredPaymentMethod'] as String? ?? '',
      netPaymentTerms: json['netPaymentTerms'] as int? ?? 30,
      outstandingBalance: (json['outstandingBalance'] as num? ?? 0).toDouble(),
      complianceNotes: json['complianceNotes'] as String?,
      isMinesite: json['isMinesite'] as bool? ?? false,
      isActive: json['isActive'] as bool? ?? true,
      createdAt: DateTime.tryParse(json['createdAt'] as String? ?? '') ?? DateTime.now(),
      activeContract: contractJson != null ? ContractModel.fromJson(contractJson) : null,
      activeContractRenewalDate: renewalDateRaw != null ? DateTime.tryParse(renewalDateRaw) : null,
      listItemIsExpiringSoon: json['isExpiringSoon'] as bool? ?? false,
      industry: json['industry'] as String?,
      projectSite: json['projectSite'] as String?,
      notificationEmails: _parseEmailList(json['notificationEmails']),
      tripDepartureArrivalEmails: _parseEmailList(json['tripDepartureArrivalEmails']),
      passengerBookingEmails: _parseEmailList(json['passengerBookingEmails']),
    );
  }

  static List<String> _parseEmailList(dynamic value) {
    if (value is! List) return const [];
    return value.map((e) => e.toString()).toList();
  }

  Map<String, dynamic> toJson() => {
        'businessName': businessName,
        'serviceType': serviceType.name[0].toUpperCase() + serviceType.name.substring(1),
        'primaryContactName': primaryContactName,
        'primaryContactTitle': primaryContactTitle,
        'phone': phone,
        'email': email,
        'streetAddress': streetAddress,
        'city': city,
        'province': province,
        'postalCode': postalCode,
        'gstHstNumber': gstHstNumber,
        'preferredPaymentMethod': preferredPaymentMethod,
        'netPaymentTerms': netPaymentTerms,
        'complianceNotes': complianceNotes,
        'isMinesite': isMinesite,
        'isActive': isActive,
        'industry': industry,
        'projectSite': projectSite,
        'notificationEmails': notificationEmails,
        'tripDepartureArrivalEmails': tripDepartureArrivalEmails,
        'passengerBookingEmails': passengerBookingEmails,
      };

  static ServiceType _parseServiceType(String value) {
    return ServiceType.values.firstWhere(
      (e) => e.name.toLowerCase() == value.toLowerCase(),
      orElse: () => ServiceType.corporate,
    );
  }
}

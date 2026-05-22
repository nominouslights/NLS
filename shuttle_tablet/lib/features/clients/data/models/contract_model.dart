import '../../domain/entities/contract.dart';
import 'contract_rate_line_model.dart';

class ContractModel extends Contract {
  const ContractModel({
    required super.id,
    required super.clientId,
    required super.startDate,
    required super.renewalDate,
    required super.isActive,
    super.notes,
    required List<ContractRateLineModel> rateLines,
  }) : super(rateLines: rateLines);

  factory ContractModel.fromJson(Map<String, dynamic> json) {
    final rawLines = json['rateLines'] as List<dynamic>? ?? [];
    return ContractModel(
      id: json['id'] as String,
      clientId: json['clientId'] as String,
      startDate: DateTime.parse(json['startDate'] as String),
      renewalDate: DateTime.parse(json['renewalDate'] as String),
      isActive: json['isActive'] as bool,
      notes: json['notes'] as String?,
      rateLines: rawLines
          .map((e) => ContractRateLineModel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }

  Map<String, dynamic> toJson() => {
        'clientId': clientId,
        'startDate': startDate.toIso8601String(),
        'renewalDate': renewalDate.toIso8601String(),
        'notes': notes,
      };
}

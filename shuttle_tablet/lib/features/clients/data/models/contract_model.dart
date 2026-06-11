import '../../domain/entities/contract.dart';
import 'contract_rate_line_model.dart';

class ContractModel extends Contract {
  const ContractModel({
    required super.id,
    required super.clientId,
    required super.startDate,
    required super.endDate,
    required super.isActive,
    super.notes,
    required List<ContractRateLineModel> rateLines,
  }) : super(rateLines: rateLines);

  /// Parses contract summary/detail JSON from the API.
  /// [clientId] is required when the payload omits it (e.g. list/summary endpoints).
  factory ContractModel.fromJson(
    Map<String, dynamic> json, {
    String? clientId,
  }) {
    final contractId = json['id'] as String;
    final rawLines = json['rateLines'] as List<dynamic>? ?? [];
    return ContractModel(
      id: contractId,
      clientId: clientId ?? json['clientId'] as String? ?? '',
      startDate: DateTime.parse(json['startDate'] as String),
      endDate: DateTime.parse(json['endDate'] as String),
      isActive: json['isActive'] as bool? ?? true,
      notes: json['notes'] as String?,
      rateLines: rawLines
          .map(
            (e) => ContractRateLineModel.fromJson(
              e as Map<String, dynamic>,
              contractId: contractId,
            ),
          )
          .toList(),
    );
  }

  Map<String, dynamic> toJson() => {
        'clientId': clientId,
        'startDate': startDate.toIso8601String(),
        'endDate': endDate.toIso8601String(),
        'notes': notes,
      };
}

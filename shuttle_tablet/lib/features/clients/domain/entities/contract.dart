import 'package:equatable/equatable.dart';
import 'contract_rate_line.dart';

class Contract extends Equatable {
  final String id;
  final String clientId;
  final DateTime startDate;
  final DateTime endDate;
  final bool isActive;
  final String? notes;
  final List<ContractRateLine> rateLines;

  const Contract({
    required this.id,
    required this.clientId,
    required this.startDate,
    required this.endDate,
    required this.isActive,
    this.notes,
    required this.rateLines,
  });

  bool get isExpiringSoon =>
      endDate.difference(DateTime.now()).inDays <= 60;

  @override
  List<Object?> get props => [id, clientId, startDate, endDate, isActive, notes, rateLines];
}

import 'package:equatable/equatable.dart';
import 'contract_rate_line.dart';

class Contract extends Equatable {
  final String id;
  final String clientId;
  final DateTime startDate;
  final DateTime renewalDate;
  final bool isActive;
  final String? notes;
  final List<ContractRateLine> rateLines;

  const Contract({
    required this.id,
    required this.clientId,
    required this.startDate,
    required this.renewalDate,
    required this.isActive,
    this.notes,
    required this.rateLines,
  });

  bool get isExpiringSoon =>
      renewalDate.difference(DateTime.now()).inDays <= 60;

  @override
  List<Object?> get props => [id, clientId, startDate, renewalDate, isActive, notes, rateLines];
}

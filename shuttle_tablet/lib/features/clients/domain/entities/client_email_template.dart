import 'package:equatable/equatable.dart';

enum ClientEmailTemplateType {
  outboundConfirmation,
  inboundConfirmation,
  departureNotification,
  arrivalNotification,
  stopUpdate,
}

extension ClientEmailTemplateTypeX on ClientEmailTemplateType {
  /// Matches the C# enum name expected by the API.
  String get apiValue {
    switch (this) {
      case ClientEmailTemplateType.outboundConfirmation:
        return 'OutboundConfirmation';
      case ClientEmailTemplateType.inboundConfirmation:
        return 'InboundConfirmation';
      case ClientEmailTemplateType.departureNotification:
        return 'DepartureNotification';
      case ClientEmailTemplateType.arrivalNotification:
        return 'ArrivalNotification';
      case ClientEmailTemplateType.stopUpdate:
        return 'StopUpdate';
    }
  }

  String get label {
    switch (this) {
      case ClientEmailTemplateType.outboundConfirmation:
        return 'Outbound Confirmation';
      case ClientEmailTemplateType.inboundConfirmation:
        return 'Inbound Confirmation';
      case ClientEmailTemplateType.departureNotification:
        return 'Departure Notification';
      case ClientEmailTemplateType.arrivalNotification:
        return 'Arrival Notification';
      case ClientEmailTemplateType.stopUpdate:
        return 'Stop Update';
    }
  }

  static ClientEmailTemplateType fromApi(String value) {
    switch (value) {
      case 'OutboundConfirmation':
        return ClientEmailTemplateType.outboundConfirmation;
      case 'InboundConfirmation':
        return ClientEmailTemplateType.inboundConfirmation;
      case 'DepartureNotification':
        return ClientEmailTemplateType.departureNotification;
      case 'ArrivalNotification':
        return ClientEmailTemplateType.arrivalNotification;
      case 'StopUpdate':
        return ClientEmailTemplateType.stopUpdate;
      default:
        throw ArgumentError('Unknown email template type: $value');
    }
  }
}

class ClientEmailTemplate extends Equatable {
  final String id;
  final ClientEmailTemplateType type;
  final String subject;
  final String body;
  final DateTime? updatedAt;

  const ClientEmailTemplate({
    required this.id,
    required this.type,
    required this.subject,
    required this.body,
    this.updatedAt,
  });

  @override
  List<Object?> get props => [id, type, subject, body, updatedAt];
}

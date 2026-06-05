import '../../domain/entities/client_email_template.dart';

class ClientEmailTemplateModel extends ClientEmailTemplate {
  const ClientEmailTemplateModel({
    required super.id,
    required super.type,
    required super.subject,
    required super.body,
    super.updatedAt,
  });

  factory ClientEmailTemplateModel.fromJson(Map<String, dynamic> json) {
    return ClientEmailTemplateModel(
      id: json['id'] as String,
      type: ClientEmailTemplateTypeX.fromApi(json['type'] as String),
      subject: json['subject'] as String? ?? '',
      body: json['body'] as String? ?? '',
      updatedAt: json['updatedAt'] != null
          ? DateTime.tryParse(json['updatedAt'] as String)
          : null,
    );
  }
}

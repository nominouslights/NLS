using NorthernLink.Shared.Messaging;

namespace NorthernLink.Notifications.Application.Templates.GetTemplateById;

/// <summary>Fetches one email template by id.</summary>
public sealed record GetEmailTemplateByIdQuery(Guid TenantId, Guid TemplateId) : IQuery<EmailTemplateResponse>;

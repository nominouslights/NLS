using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Domain;

namespace NorthernLink.Notifications.Application.Templates.Update;

/// <summary>Replaces an email template's content.</summary>
public sealed record UpdateEmailTemplateCommand(
    Guid TenantId,
    Guid TemplateId,
    string Name,
    NotificationServiceType ServiceType,
    Guid? ClientId,
    string? ClientName,
    string Subject,
    string HtmlBody) : ICommand;

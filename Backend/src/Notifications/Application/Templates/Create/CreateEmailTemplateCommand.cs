using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Domain;

namespace NorthernLink.Notifications.Application.Templates.Create;

/// <summary>Creates a new (active) email template. Returns the new template's id.</summary>
public sealed record CreateEmailTemplateCommand(
    Guid TenantId,
    string Name,
    NotificationServiceType ServiceType,
    Guid? ClientId,
    string? ClientName,
    string Subject,
    string HtmlBody) : ICommand<Guid>;

using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Domain;

namespace NorthernLink.Notifications.Application.Templates.GetTemplates;

/// <summary>
/// Lists the tenant's email templates, optionally filtered by service type and/or client;
/// inactive templates only appear when <paramref name="IncludeInactive"/> is set.
/// </summary>
public sealed record GetEmailTemplatesQuery(
    Guid TenantId,
    NotificationServiceType? ServiceType,
    Guid? ClientId,
    bool IncludeInactive) : IQuery<IReadOnlyList<EmailTemplateResponse>>;

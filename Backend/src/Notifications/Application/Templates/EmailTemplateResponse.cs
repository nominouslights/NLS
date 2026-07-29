namespace NorthernLink.Notifications.Application.Templates;

/// <summary>
/// The Notifications module's public representation of an email template — the shape the
/// Communications screen and the send dialog consume. <paramref name="ServiceType"/> is the
/// <c>NotificationServiceType</c> name as a string (ContractCrew, Community, Nihb, Charter,
/// Cargo, Grocery). <paramref name="ClientId"/>/<paramref name="ClientName"/> are set only
/// on client-specific templates.
/// </summary>
public sealed record EmailTemplateResponse(
    Guid Id,
    string Name,
    string ServiceType,
    Guid? ClientId,
    string? ClientName,
    string Subject,
    string HtmlBody,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

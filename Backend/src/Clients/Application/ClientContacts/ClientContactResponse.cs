namespace NorthernLink.Clients.Application.ClientContacts;

/// <summary>
/// The Clients module's public representation of a client contact — the shape every
/// frontend consumes. IsPrimary is the primary-contact flag for this organization.
/// </summary>
public sealed record ClientContactResponse(
    Guid Id,
    Guid ClientId,
    string Name,
    string Title,
    string? Email,
    string? Phone,
    string? Notes,
    bool IsPrimary,
    bool ReceivesEmailReports,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.ClientContacts.Create;

/// <summary>Creates a new client contact. Returns the new contact's id.</summary>
public sealed record CreateClientContactCommand(
    Guid TenantId,
    Guid ClientId,
    string Name,
    string Title,
    string? Email,
    string? Phone,
    string? Notes,
    bool IsPrimary,
    bool ReceivesEmailReports,
    bool ReceivesAccrualsReports) : ICommand<Guid>;

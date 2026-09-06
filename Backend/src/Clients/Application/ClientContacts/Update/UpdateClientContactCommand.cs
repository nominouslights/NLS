using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.ClientContacts.Update;

/// <summary>
/// Replaces every editable field of an existing client contact (full-document PUT
/// semantics, mirroring <c>CreateClientContactCommand</c>'s field set).
/// </summary>
public sealed record UpdateClientContactCommand(
    Guid TenantId,
    Guid ClientId,
    Guid ContactId,
    string Name,
    string Title,
    string? Email,
    string? Phone,
    string? Notes,
    bool IsPrimary,
    bool ReceivesEmailReports,
    bool ReceivesAccrualsReports) : ICommand;

using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.ClientContacts.GetForClient;

/// <summary>
/// Queries the contacts for a client (read side), ordered by primary first, then by creation time.
/// </summary>
public sealed record GetClientContactsQuery(Guid TenantId, Guid ClientId)
    : IQuery<IReadOnlyList<ClientContactResponse>>;

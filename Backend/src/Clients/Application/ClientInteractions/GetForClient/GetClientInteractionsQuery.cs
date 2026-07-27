using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.ClientInteractions.GetForClient;

/// <summary>Every interaction logged for one client, most recent first.</summary>
public sealed record GetClientInteractionsQuery(Guid TenantId, Guid ClientId)
    : IQuery<IReadOnlyList<ClientInteractionResponse>>;

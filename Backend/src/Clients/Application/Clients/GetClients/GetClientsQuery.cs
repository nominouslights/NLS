using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.Clients.GetClients;

/// <summary>Lists every client organization visible to the current tenant.</summary>
public sealed record GetClientsQuery(Guid TenantId) : IQuery<IReadOnlyList<ClientResponse>>;

using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.Clients.GetClientById;

/// <summary>A single client with its embedded active-contract summary.</summary>
public sealed record GetClientByIdQuery(Guid TenantId, Guid ClientId) : IQuery<ClientResponse>;

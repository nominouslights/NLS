using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.Contracts.GetForClient;

/// <summary>Every contract (active, ended, terminated) of one client, newest period first.</summary>
public sealed record GetClientContractsQuery(Guid TenantId, Guid ClientId)
    : IQuery<IReadOnlyList<ContractResponse>>;

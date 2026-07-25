using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.Contracts.Events;

/// <summary>Raised when a contract is created (contracts are born Active).</summary>
public sealed record ContractActivatedDomainEvent(Guid ContractId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

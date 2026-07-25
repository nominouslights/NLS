using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.Contracts.Events;

/// <summary>Raised when a contract is cut short by termination.</summary>
public sealed record ContractTerminatedDomainEvent(Guid ContractId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

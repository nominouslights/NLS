using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.Contracts.Events;

/// <summary>Raised when an active contract's terms are amended.</summary>
public sealed record ContractUpdatedDomainEvent(Guid ContractId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

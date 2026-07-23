using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.Clients.Events;

/// <summary>Raised when a client organization is renamed or recategorized.</summary>
public sealed record ClientUpdatedDomainEvent(Guid ClientId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.Clients.Events;

/// <summary>Raised when a client organization is first created.</summary>
public sealed record ClientCreatedDomainEvent(Guid ClientId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

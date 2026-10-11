using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Qbo.Events;

/// <summary>Raised when a person disconnects QuickBooks. <paramref name="ActorId"/> is from the signed token.</summary>
public sealed record QboDisconnectedDomainEvent(
    Guid ConnectionId,
    Guid TenantId,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

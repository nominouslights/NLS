using NorthernLink.Shared.Kernel;

namespace NorthernLink.Notifications.Domain.Dispatches.Events;

/// <summary>Raised when a send action's outcomes are recorded as history.</summary>
public sealed record EmailDispatchRecordedDomainEvent(Guid DispatchId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Drivers.Events;

/// <summary>
/// Raised when a roster row's Identity link is broken — that account can no longer act as this
/// driver. Carries the user id that was removed, because the row itself no longer holds it and
/// "which login lost driver access" is exactly the question an audit asks.
/// </summary>
public sealed record DriverUserUnlinkedDomainEvent(Guid DriverId, Guid PreviousUserId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

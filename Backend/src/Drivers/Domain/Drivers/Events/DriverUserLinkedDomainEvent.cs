using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Drivers.Events;

/// <summary>
/// Raised when a roster row is linked to an Identity user — i.e. when that account gains the
/// ability to act as this driver. Its own event rather than a <c>DriverUpdatedDomainEvent</c>
/// precisely because it is an access change: the journal must show who was granted a driver's
/// identity and when, distinctly from "someone corrected a licence class".
/// </summary>
public sealed record DriverUserLinkedDomainEvent(Guid DriverId, Guid UserId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

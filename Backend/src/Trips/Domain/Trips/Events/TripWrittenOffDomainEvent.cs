using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when Billing writes off the invoice carrying this trip — the money is not coming.
/// Internal to Trips and deliberately unmapped: the fact originated in Billing, so publishing it
/// would echo Billing's own event back at it. The dispatcher-driven counterpart is
/// <see cref="TripClosedWithoutBillingDomainEvent"/>, which <em>does</em> publish, because that
/// write-off is news Billing hasn't heard.
/// </summary>
public sealed record TripWrittenOffDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

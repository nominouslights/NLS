using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a <em>client</em> trip's run ends and it becomes billable — the publish hook for
/// the <c>trips.trip-ready-for-billing</c> integration event Billing consumes to record a
/// billable trip. Replaces <see cref="TripCompletedDomainEvent"/> in that role: under the
/// billing-driven lifecycle a trip reaching Completed means the money arrived, which is far too
/// late to start drafting a worksheet.
/// <para>
/// Never raised for a clientless run (community, walk-up charter) — those finish straight into
/// Completed and no invoice is ever drafted for them.
/// </para>
/// </summary>
public sealed record TripReadyForBillingDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

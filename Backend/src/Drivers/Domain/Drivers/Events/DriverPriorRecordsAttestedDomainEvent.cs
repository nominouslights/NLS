using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Drivers.Events;

/// <summary>
/// Raised when a compliance officer attests that the driver's prior records (s.82(1)(f) — the
/// previous 14 days, from their last carrier or on paper) are on file through
/// <see cref="CoveredThroughDate"/>. Maps to <c>drivers.driver-changed</c>, which carries the
/// attestation fields, so the Trips replica's hours gate learns the records are complete.
/// </summary>
public sealed record DriverPriorRecordsAttestedDomainEvent(
    Guid DriverId,
    DateOnly CoveredThroughDate,
    bool WorksForOtherCarriers) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

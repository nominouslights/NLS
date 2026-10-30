using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a dispatcher corrects the driver recorded on a trip whose run is already over
/// (<see cref="Trip.CorrectDriver"/>) — fixing history, not reassigning live work. The whole
/// record is what the event journal stores (journal name <c>trip-driver-corrected</c>), so it is
/// also the audit trail: the activity timeline reads <see cref="Reason"/> and
/// <see cref="EnteredBy"/> straight out of the journaled payload and builds its "Driver: A → B"
/// detail from the two names. The trip itself keeps neither the reason nor the previous driver.
/// <para>
/// <see cref="EnteredByUserId"/>/<see cref="EnteredBy"/> come from the signed access token
/// (<c>ICurrentActor</c>), never from the request body. Maps to
/// <c>trips.trip-driver-corrected</c>.
/// </para>
/// </summary>
public sealed record TripDriverCorrectedDomainEvent(
    Guid TripId,
    Guid? PreviousDriverId,
    string? PreviousDriverName,
    Guid NewDriverId,
    string NewDriverName,
    string Reason,
    Guid? EnteredByUserId,
    string? EnteredBy) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

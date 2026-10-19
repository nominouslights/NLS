using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Trips;

/// <summary>
/// Published when a dispatcher corrects the driver recorded on a trip whose run is already over
/// (ReadyForBilling, Invoiced, or Completed) — routing key <c>trips.trip-driver-corrected</c>.
/// A correction of history, not a reassignment: the trip's status, vehicle, and billing are
/// untouched.
/// <para>
/// No module consumes it yet. It is on the wire so a future consumer (driver-hours or payroll
/// reconciliation, a reporting replica) can learn of corrections without reading Trips. It is a
/// storing/projecting fact, so it travels by outbox polling like every other Trips event; rows sit
/// Pending in the Trips outbox until a consumer subscribes, which then replays them in
/// order.
/// </para>
/// <para>
/// <see cref="CorrectedByUserId"/>/<see cref="CorrectedByEmail"/> come from the signed access
/// token server-side, never the request body, and are null only when no principal was present.
/// <see cref="PreviousDriverId"/>/<see cref="PreviousDriverName"/> are null when the trip had no
/// driver on record. The base <see cref="IntegrationEvent.OccurredAtUtc"/> is the moment of the
/// correction. <see cref="TenantId"/> is in the payload because handlers run outside any HTTP
/// request.
/// </para>
/// </summary>
public sealed record TripDriverCorrectedIntegrationEvent(
    Guid TripId,
    Guid TenantId,
    string TripNumber,
    DateOnly ServiceDate,
    Guid? PreviousDriverId,
    string? PreviousDriverName,
    Guid NewDriverId,
    string NewDriverName,
    string Reason,
    Guid? CorrectedByUserId,
    string? CorrectedByEmail) : IntegrationEvent;

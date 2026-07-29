using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.GetActivity;

/// <summary>
/// The per-trip activity timeline: the trip's own journaled lifecycle events unioned with
/// its attached manifest's journaled create/edit events, ordered oldest-first. Read-only —
/// sourced from the module's append-only <c>event_journal</c>, never a projection.
/// </summary>
public sealed record GetTripActivityQuery(Guid TripId, Guid TenantId)
    : IQuery<IReadOnlyList<TripActivityEntryResponse>>;

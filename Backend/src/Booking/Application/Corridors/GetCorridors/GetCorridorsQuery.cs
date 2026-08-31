using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Corridors.GetCorridors;

/// <summary>
/// The bookable corridors — Booking's replica of Trips routes (corridor_lookup), inactive
/// ones included so the frontend can gray them out. Empty until routes are re-saved once
/// after this module deploys (the replica bootstraps from trips.route-changed events).
/// </summary>
public sealed record GetCorridorsQuery(Guid TenantId) : IQuery<IReadOnlyList<CorridorResponse>>;

/// <summary>One bookable corridor (= a Trips route, replicated by id + name snapshots).</summary>
public sealed record CorridorResponse(
    Guid CorridorId,
    string Name,
    string Origin,
    string Destination,
    bool Active);

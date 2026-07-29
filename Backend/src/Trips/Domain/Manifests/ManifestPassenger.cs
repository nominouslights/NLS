namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>
/// One §5 passenger row (the form has space for eight). Persisted as jsonb.
/// Pickup/dropoff are snapshot references to the trip's route stops: when the passenger
/// boards at a catalogued stop, <see cref="PickupStopId"/> carries the
/// <c>RouteStop.StopId</c> and <see cref="PickupStopName"/> the snapshot name; a
/// free-form trip with no route leaves the ids null and may carry just the names (or
/// nothing). No hard cross-aggregate validation — this is a snapshot, like the route
/// stops on the trip itself.
/// </summary>
public sealed record ManifestPassenger
{
    public required string Name { get; init; }
    public string? Contact { get; init; }

    /// <summary>Catalog stop this passenger boarded at — null for free-form trips.</summary>
    public Guid? PickupStopId { get; init; }

    /// <summary>Snapshot of the pickup stop name (or a free-text pickup) — null when unknown.</summary>
    public string? PickupStopName { get; init; }

    /// <summary>Catalog stop this passenger got off at — null for free-form trips.</summary>
    public Guid? DropoffStopId { get; init; }

    /// <summary>Snapshot of the dropoff stop name (or a free-text dropoff) — null when unknown.</summary>
    public string? DropoffStopName { get; init; }

    public bool IdVerified { get; init; }
    public bool BoardedOn { get; init; }
    public bool BoardedOff { get; init; }
}

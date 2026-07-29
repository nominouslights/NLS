namespace NorthernLink.Trips.Domain.Routes;

/// <summary>
/// One ordered stop on a route, from origin (lowest <see cref="Order"/>) to destination
/// (highest). Persisted as jsonb on the owning aggregate — stops are pure values with no
/// identity of their own. When built from the catalog it snapshots the referencing
/// <see cref="Stop"/>'s id and coordinates; legacy free-text stops leave those null and
/// degrade gracefully (no pin on the map).
/// </summary>
public sealed record RouteStop
{
    /// <summary>Catalog Stop this snapshot came from — null for legacy free-text stops.</summary>
    public Guid? StopId { get; init; }

    /// <summary>Snapshot of the stop name at the time the route was saved.</summary>
    public required string Name { get; init; }

    public int Order { get; init; }

    /// <summary>Snapshot of the stop coordinate — feeds the Live Map. Null for legacy stops.</summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}

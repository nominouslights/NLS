namespace NorthernLink.Trips.Domain.Routes;

/// <summary>
/// One named stop on a route, ordered from origin (lowest <see cref="Order"/>) to
/// destination (highest). Persisted as jsonb on the owning aggregate — stops are pure
/// values with no identity of their own.
/// </summary>
public sealed record RouteStop
{
    public required string Name { get; init; }
    public int Order { get; init; }
}

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

    /// <summary>
    /// Timetable: minutes after the OUTBOUND leg's departure that the vehicle reaches this
    /// stop. The outbound leg runs in ascending <see cref="Order"/>, so the first stop is 0.
    /// Null on every stop when the route has no outbound timetable (the pre-timetable
    /// default) — a leg's offsets are all-or-nothing, never partially filled.
    /// </summary>
    public int? OutboundOffsetMinutes { get; init; }

    /// <summary>
    /// Timetable: minutes after the RETURN leg's departure. The return runs the stops in
    /// reverse, so the <em>last</em> stop by <see cref="Order"/> is the return's 0 and offsets
    /// grow as <see cref="Order"/> descends. Null throughout when the route has no return
    /// timetable.
    /// </summary>
    /// <remarks>
    /// Both offsets stay attached to their stop for the whole life of the snapshot — including
    /// onto trips, where the leg's <c>TripDirection</c> selects which one applies. That is why
    /// reversing a stop list (inbound generation, deadhead returns) copies both through
    /// unchanged rather than swapping them.
    /// </remarks>
    public int? ReturnOffsetMinutes { get; init; }
}

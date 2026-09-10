namespace NorthernLink.Booking.Application.Integration;

/// <summary>
/// Booking's replica of a Trips route, maintained by upserting
/// <c>RouteChangedIntegrationEvent</c>s — never joined from the Trips module (domain
/// libraries never reference each other). A plain keyed row (not an aggregate: no audit
/// journal, no events), used to validate a booking's corridor, snapshot its name, and
/// list corridors for the calendar's selector. Mirrors Trips' <c>VehicleLookup</c>.
/// Seeded from <c>trips.rm_routes</c> by the <c>BackfillCorridorLookup</c> migration;
/// live <c>trips.route-changed</c> events keep it current.
/// </summary>
public sealed class CorridorLookup
{
    public Guid CorridorId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string Origin { get; set; } = null!;
    public string Destination { get; set; } = null!;
    public bool Active { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

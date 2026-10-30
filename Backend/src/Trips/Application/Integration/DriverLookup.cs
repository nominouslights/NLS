namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Trips' replica of a driver, maintained by upserting
/// <c>DriverChangedIntegrationEvent</c>s — never joined from the Drivers module (domain
/// libraries never reference each other). A plain keyed row (not an aggregate: no audit
/// journal, no events), used to validate driver assignment, snapshot the driver's name
/// onto trips, and resolve a Driver Field App caller to the driver they are. Status travels
/// as the event's string form ("Active", "Inactive", "Deactivated").
/// </summary>
public sealed class DriverLookup
{
    public const string ActiveStatus = "Active";

    public Guid DriverId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string LicenceClass { get; set; } = null!;
    public string Status { get; set; } = null!;

    /// <summary>
    /// The Identity user (<c>sub</c>) linked to this driver in the Drivers module, or null
    /// when nobody can sign in as them. This is what lets the driver-facing <c>/api/trips</c>
    /// routes answer "is this the caller's own trip?" — the Trips half of the
    /// caller-owns-this-row check. Unique per tenant (partial index), mirroring
    /// <c>drivers.drivers.user_id</c>.
    /// </summary>
    public Guid? UserId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool IsActive => Status == ActiveStatus;
}

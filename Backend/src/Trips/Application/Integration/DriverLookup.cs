namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Trips' replica of a driver, maintained by upserting
/// <c>DriverChangedIntegrationEvent</c>s — never joined from the Drivers module (domain
/// libraries never reference each other). A plain keyed row (not an aggregate: no audit
/// journal, no events), used to validate driver assignment and snapshot the driver's
/// name onto trips. Status travels as the event's string form ("Active", "Inactive",
/// "Deactivated").
/// </summary>
public sealed class DriverLookup
{
    public const string ActiveStatus = "Active";

    public Guid DriverId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string LicenceClass { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool IsActive => Status == ActiveStatus;
}

namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Trips' replica of a Fleet vehicle, maintained by upserting
/// <c>VehicleChangedIntegrationEvent</c>s — never joined from the Fleet module (domain
/// libraries never reference each other). A plain keyed row (not an aggregate: no audit
/// journal, no events), used to validate vehicle assignment and snapshot the vehicle's
/// unit number onto trips. Status travels as the event's string form ("Active",
/// "InMaintenance", "OutOfService", "Retired", "Sold", "Recycled"). Mirrors
/// <see cref="DriverLookup"/>.
/// </summary>
public sealed class VehicleLookup
{
    public const string ActiveStatus = "Active";

    public Guid VehicleId { get; set; }
    public Guid TenantId { get; set; }
    public string UnitNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string RequiredLicenceClass { get; set; } = null!;
    public int SeatingCapacity { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool IsActive => Status == ActiveStatus;
}

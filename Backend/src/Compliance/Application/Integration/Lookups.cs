using NorthernLink.Compliance.Domain.Authority;

namespace NorthernLink.Compliance.Application.Integration;

/// <summary>
/// Compliance's replica of a Fleet vehicle, upserted from <c>VehicleChangedIntegrationEvent</c> —
/// never joined from Fleet (domain libraries never reference each other). A plain keyed row, not
/// an aggregate. Every column the event carries is overwritten on each upsert;
/// <see cref="RegulatedOverride"/> is Compliance's own and the upsert never touches it.
/// </summary>
public sealed class VehicleLookup
{
    public Guid VehicleId { get; set; }
    public Guid TenantId { get; set; }
    public string UnitNumber { get; set; } = null!;

    /// <summary>Fleet's status name: Active, InMaintenance, OutOfService, Retired, Sold, Recycled.</summary>
    public string Status { get; set; } = null!;

    public int SeatingCapacity { get; set; }
    public string RequiredLicenceClass { get; set; } = null!;

    /// <summary>
    /// A compliance officer's decision that this vehicle is (true) or is not (false) a regulated
    /// vehicle regardless of its seating; null = derive from seating (owner question Q1).
    /// </summary>
    public bool? RegulatedOverride { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool IsRegulated(int minSeatingCapacity) =>
        VehicleRegulation.IsRegulated(SeatingCapacity, RegulatedOverride, minSeatingCapacity);

    public bool CountsForReadiness => VehicleRegulation.CountsForReadiness(Status);
}

/// <summary>
/// Compliance's replica of a driver, upserted from <c>DriverChangedIntegrationEvent</c>: enough to
/// name a driver and to match a signed-in Driver to the acknowledgements owed (via <see cref="UserId"/>).
/// </summary>
public sealed class DriverLookup
{
    public Guid DriverId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>Drivers' status name: Active, Inactive, Deactivated.</summary>
    public string Status { get; set; } = null!;

    public Guid? UserId { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Compliance's replica of a platform account, upserted from <c>UserChangedIntegrationEvent</c> —
/// how a signature's <c>SignerUserId</c> or a designation's person resolves to a readable name.
/// <see cref="FullName"/> is the display value; <see cref="Email"/> the fallback.
/// </summary>
public sealed class UserLookup
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = null!;
    public string? FullName { get; set; }
    public string Role { get; set; } = null!;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

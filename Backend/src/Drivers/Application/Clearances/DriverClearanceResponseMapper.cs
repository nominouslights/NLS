using NorthernLink.Drivers.Domain.Clearances;

namespace NorthernLink.Drivers.Application.Clearances;

/// <summary>Maps the DriverClearance aggregate to the module's public response contract.</summary>
public static class DriverClearanceResponseMapper
{
    public static DriverClearanceResponse ToResponse(DriverClearance clearance) => new(
        clearance.Id,
        clearance.DriverId,
        clearance.Title,
        clearance.ClientName,
        clearance.Expiry,
        clearance.GrantedAtUtc);
}

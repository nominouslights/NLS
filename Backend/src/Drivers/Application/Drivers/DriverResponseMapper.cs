using NorthernLink.Drivers.Domain.Drivers;

namespace NorthernLink.Drivers.Application.Drivers;

/// <summary>Maps the Driver aggregate to the module's public response contract.</summary>
public static class DriverResponseMapper
{
    /// <summary>
    /// The credential fields are denormalized read-side state the aggregate doesn't own,
    /// so the caller supplies them (the projection computes them for rm_drivers).
    /// </summary>
    public static DriverResponse ToResponse(
        Driver driver,
        int credentialCount,
        DateOnly? soonestCredentialExpiry,
        string? latestDutyStatus,
        decimal? latestDrivingHours,
        DateOnly? latestHosDate) => new(
        driver.Id,
        driver.Name,
        driver.Phone,
        driver.LicenceClass,
        driver.LicenceExpiry,
        driver.Source,
        driver.HasWorkPermit,
        driver.Status.ToString(),
        credentialCount,
        soonestCredentialExpiry,
        latestDutyStatus,
        latestDrivingHours,
        latestHosDate,
        driver.RegisteredAtUtc,
        driver.UpdatedAtUtc);
}

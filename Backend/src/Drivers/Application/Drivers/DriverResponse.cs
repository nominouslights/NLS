namespace NorthernLink.Drivers.Application.Drivers;

/// <summary>
/// The Drivers module's public representation of a driver — the shape every frontend
/// consumes. <paramref name="Status"/> is the <c>DriverStatus</c> name as a string
/// (Active, Inactive, Deactivated). <paramref name="CredentialCount"/> and
/// <paramref name="SoonestCredentialExpiry"/> are denormalized from the driver's
/// credentials so roster lists need one call; expiry status chips are derived by the
/// frontend from the dates, never here. <paramref name="LatestDutyStatus"/> (the friendly
/// duty string) and <paramref name="LatestDrivingHours"/> are the same kind of roster
/// rollup from the driver's newest HOS entry — the roster's duty chip and "HOS left"
/// column read them without a per-driver HOS call. All three are null until the first
/// HOS entry.
/// </summary>
public sealed record DriverResponse(
    Guid Id,
    string Name,
    string? Phone,
    string LicenceClass,
    DateOnly? LicenceExpiry,
    string Source,
    bool HasWorkPermit,
    string Status,
    int CredentialCount,
    DateOnly? SoonestCredentialExpiry,
    string? LatestDutyStatus,
    decimal? LatestDrivingHours,
    DateOnly? LatestHosDate,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset UpdatedAtUtc);

namespace NorthernLink.Drivers.Application.Hos;

/// <summary>
/// The Drivers module's public representation of an HOS log entry. <paramref name="Duty"/>
/// and <paramref name="Source"/> are the friendly display strings the frontend keys on
/// ("Off Duty"/"On Duty"/"Driving"; "Driver App"/"Manual (paper backup)") — produced at the
/// boundary by <c>HosDisplay</c>; the database stores the clean enums. The CVDHS remaining/
/// violation gauge is derived by the frontend from these raw hours, never here.
/// </summary>
public sealed record HosEntryResponse(
    Guid Id,
    Guid DriverId,
    DateOnly Date,
    string Duty,
    decimal OnDutyH,
    decimal DrivingH,
    decimal OffDutyH,
    string Source,
    string? EnteredBy,
    string? Note,
    DateTimeOffset RecordedAtUtc);

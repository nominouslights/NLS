namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// The duty state a driver was in for a logged Hours-of-Service period. The friendly
/// wire strings ("Off Duty" / "On Duty" / "Driving") are produced at the response
/// boundary by <c>HosDisplay</c>; the database stores this clean enum.
/// </summary>
public enum DutyStatus
{
    OffDuty,
    OnDuty,
    Driving,
}

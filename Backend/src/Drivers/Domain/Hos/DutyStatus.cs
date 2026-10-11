namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// The duty state a driver was in for a logged Hours-of-Service period. The friendly
/// wire strings ("Off Duty" / "On Duty" / "Driving" / "Personal Conveyance") are produced at
/// the response boundary by <c>HosDisplay</c> (delegating to the engine's <c>HosWire</c>); the
/// database stores this clean enum. Member spellings match the rule engine's
/// <c>NorthernLink.Shared.HoursOfService.DutyStatus</c> one for one — <c>HosDisplay</c> maps
/// between them by name.
/// </summary>
public enum DutyStatus
{
    OffDuty,
    OnDuty,
    Driving,

    /// <summary>
    /// Personal use of the vehicle (s.1 "personal use"): off duty for every rest rule, but
    /// capped at 75.0 km per day, past which the remainder is work. A <c>DutyEvent</c> opening
    /// this status must carry a start odometer and the three attestations; the event closing it
    /// must carry the end odometer.
    /// </summary>
    PersonalConveyance,
}

namespace NorthernLink.Drivers.Domain.Drivers;

/// <summary>
/// Driver lifecycle. Active drivers are assignable to trips; Inactive is a temporary
/// pause (leave, seasonal); Deactivated is the off-roster terminal-ish state — a
/// deactivated driver must be reinstated to Active before any other transition.
/// </summary>
public enum DriverStatus
{
    Active,
    Inactive,
    Deactivated,
}

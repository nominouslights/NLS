namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// Criticality tier of a maintained component. Primary components are safety- or
/// mission-critical; Secondary components support them.
/// </summary>
public enum ComponentTier
{
    Primary,
    Secondary,
}

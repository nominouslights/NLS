namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// Pre-trip weather checkboxes — more than one may apply on a northern run. Fleet-owned
/// so the module stays self-contained (never references the Trips manifest's enums).
/// </summary>
public enum InspectionWeather
{
    Clear,
    Cloudy,
    Rain,
    Snow,
    Fog,
    ExtremeCold,
}

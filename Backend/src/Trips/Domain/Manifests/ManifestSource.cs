namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>
/// How a manifest entered the system: submitted from the Driver Field App, or a printed
/// blank form transcribed by a dispatcher after a device failure (paper provenance).
/// </summary>
public enum ManifestSource
{
    App,
    Paper,
}

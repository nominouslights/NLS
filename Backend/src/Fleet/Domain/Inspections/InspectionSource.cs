namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// How the originating manifest entered the system: submitted from the Driver Field App,
/// or transcribed from a paper form by a dispatcher.
/// </summary>
public enum InspectionSource
{
    DriverApp,
    PaperTranscription,
}

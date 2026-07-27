namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// How an inspection entered the system: submitted from the Driver Field App, or entered
/// by a dispatcher from the trip workflow (the office path, including paper transcription).
/// </summary>
public enum InspectionSource
{
    DriverApp,
    Dispatcher,
}

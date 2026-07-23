namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// Where an Hours-of-Service entry came from: submitted from the Driver Field App (the
/// primary source, ingested via an integration event — deferred), or typed into the
/// dispatch console by a dispatcher when the app was unavailable (the paper backup).
/// Mirrors Fleet's <c>InspectionSource</c> two-source model.
/// </summary>
public enum HosLogEntrySource
{
    DriverApp,
    ManualPaperBackup,
}

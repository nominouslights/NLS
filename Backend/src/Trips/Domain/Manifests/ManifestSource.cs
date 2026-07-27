namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>
/// How a manifest entered the system: submitted from the Driver Field App, or entered by
/// a dispatcher (a transcribed paper form, or a manifest built/edited in the Dispatch
/// Console). Stamped on every create and edit for source attribution in the audit log.
/// </summary>
public enum ManifestSource
{
    App,
    Dispatcher,
}

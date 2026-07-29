namespace NorthernLink.Notifications.Domain;

/// <summary>
/// The service category a template targets. Declared per-module (never shared) — same
/// PascalCase members and wire strings as the Clients/Trips copies, so frontends reuse
/// one string type, but no module references another's enum.
/// </summary>
public enum NotificationServiceType
{
    ContractCrew,
    Community,
    Nihb,
    Charter,
    Cargo,
    Grocery,
}

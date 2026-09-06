namespace NorthernLink.Notifications.Domain;

/// <summary>
/// The service category a template targets. Declared per-module (never shared) — the first
/// six members keep the same PascalCase members and wire strings as the Clients/Trips
/// copies, so frontends reuse one string type, but no module references another's enum.
/// <see cref="CommunityBookingAtRisk"/> is Notifications-only (it is a notification
/// category, not a trip service type): it tags the automated "trip at risk" emails sent
/// when a booking day reverts, and an active template of this type overrides their
/// built-in body. Never add it to TripServiceType — that enum's spellings are mirrored by
/// Budgeting frontend tests.
/// </summary>
public enum NotificationServiceType
{
    ContractCrew,
    Community,
    Nihb,
    Charter,
    Cargo,
    Grocery,
    CommunityBookingAtRisk,
}

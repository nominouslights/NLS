using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Domain;

namespace NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;

/// <summary>
/// Sends the pickup email for one trip to selected manifest passengers and records the
/// per-recipient outcomes as history. <paramref name="DispatchId"/> is client-generated —
/// replaying the same id returns the stored dispatch without re-sending. Trip context
/// (<paramref name="TripId"/>, <paramref name="TripNumber"/>, <paramref name="ManifestId"/>,
/// <paramref name="ClientId"/>, <paramref name="ClientName"/>, the formatted
/// <paramref name="TripDate"/> / <paramref name="PickupTime"/> / <paramref name="Route"/>)
/// arrives as opaque snapshots composed by the dispatcher's screen — Notifications never
/// queries Trips or Clients. <paramref name="ClientId"/> (null = client-less trip) is
/// validated against the template's client pin before anything is sent.
/// </summary>
public sealed record SendTripPickupEmailCommand(
    Guid TenantId,
    Guid DispatchId,
    Guid TemplateId,
    Guid TripId,
    string TripNumber,
    Guid? ManifestId,
    NotificationServiceType ServiceType,
    string TripDate,
    string PickupTime,
    string DropoffTime,
    string Route,
    Guid? ClientId,
    string? ClientName,
    IReadOnlyList<RecipientInput> Recipients,
    IReadOnlyList<string> ReportRecipients) : ICommand<EmailDispatchResponse>;

/// <summary>
/// One selected manifest passenger: address plus the passenger's merge values.
/// <paramref name="PickupTime"/> and <paramref name="DropoffTime"/> are this passenger's own
/// times, resolved by the dispatcher's screen from the route timetable and their boarding stop —
/// on a corridor run the passenger boarding mid-route is picked up well after the vehicle departs.
/// Null (the case for an untimed route or a free-form trip) falls back to the command's
/// trip-level <c>PickupTime</c>/<c>DropoffTime</c>, which is what every recipient used before
/// timetables existed.
/// </summary>
public sealed record RecipientInput(
    string Email,
    string PassengerName,
    string? PickupStop,
    string? PickupAddress,
    string? DropoffStop,
    string? DropoffStopAddress,
    string? PickupTime = null,
    string? DropoffTime = null);

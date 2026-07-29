using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Domain;

namespace NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;

/// <summary>
/// Sends the pickup email for one trip to selected manifest passengers and records the
/// per-recipient outcomes as history. <paramref name="DispatchId"/> is client-generated —
/// replaying the same id returns the stored dispatch without re-sending. Trip context
/// (<paramref name="TripId"/>, <paramref name="TripNumber"/>, <paramref name="ManifestId"/>,
/// <paramref name="ClientName"/>, the formatted <paramref name="TripDate"/> /
/// <paramref name="PickupTime"/> / <paramref name="Route"/>) arrives as opaque snapshots
/// composed by the dispatcher's screen — Notifications never queries Trips or Clients.
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
    string Route,
    string? ClientName,
    IReadOnlyList<RecipientInput> Recipients) : ICommand<EmailDispatchResponse>;

/// <summary>One selected manifest passenger: address plus the passenger's merge values.</summary>
public sealed record RecipientInput(
    string Email,
    string PassengerName,
    string? PickupStop,
    string? DropoffStop);

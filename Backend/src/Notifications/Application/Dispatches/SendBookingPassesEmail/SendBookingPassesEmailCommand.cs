using NorthernLink.Shared.Messaging;

namespace NorthernLink.Notifications.Application.Dispatches.SendBookingPassesEmail;

/// <summary>
/// Emails a community booking's passes (HTML only — one pass block per traveller, no PDF)
/// to the customer and records the per-recipient outcomes as history.
/// <paramref name="DispatchId"/> is client-generated — replaying the same id returns the
/// stored dispatch without re-sending. <paramref name="BookingId"/> +
/// <paramref name="BookingReference"/> anchor the history row; the <paramref name="Sheet"/>
/// arrives fully composed by the dispatcher's booking detail screen — Notifications never
/// queries Booking. Recipients are the pre-resolved addresses (1–16 after de-duplication).
/// </summary>
public sealed record SendBookingPassesEmailCommand(
    Guid TenantId,
    Guid DispatchId,
    Guid BookingId,
    string BookingReference,
    BookingPassSheet Sheet,
    IReadOnlyList<PassRecipientInput> Recipients) : ICommand<EmailDispatchResponse>;

/// <summary>One selected recipient: address plus the display name recorded in history.</summary>
public sealed record PassRecipientInput(string Email, string ContactName);

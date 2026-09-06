using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Domain;

namespace NorthernLink.Notifications.Application.Dispatches.SendClientAccrualsEmail;

/// <summary>
/// Sends one client's accruals report (as a PDF attachment) to the client's flagged contacts
/// and records the per-recipient outcomes as history. <paramref name="DispatchId"/> is
/// client-generated — replaying the same id returns the stored dispatch without re-sending.
/// The <paramref name="Report"/> and the client snapshot (<paramref name="ClientId"/>,
/// <paramref name="ClientName"/>, <paramref name="ServiceType"/>) arrive fully composed by the
/// dispatcher's Reports screen — Notifications never queries Trips, Billing, or Clients.
/// Recipients are the pre-resolved contact addresses (1–16 after de-duplication).
/// </summary>
public sealed record SendClientAccrualsEmailCommand(
    Guid TenantId,
    Guid DispatchId,
    Guid ClientId,
    string ClientName,
    NotificationServiceType ServiceType,
    ClientAccrualsReport Report,
    IReadOnlyList<AccrualsRecipientInput> Recipients) : ICommand<EmailDispatchResponse>;

/// <summary>One selected client contact: address plus the display name recorded in history.</summary>
public sealed record AccrualsRecipientInput(string Email, string ContactName);

using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Dispatches.SendBookingPassesEmail;

namespace NorthernLink.Notifications.Application.Dispatches.PreviewBookingPassesEmail;

/// <summary>
/// Previews the booking-passes email without sending anything: composes through the exact
/// same <see cref="BookingPassesEmailComposer"/> and <see cref="BookingPassesRecipientGate"/>
/// the send path uses, so what a dispatcher sees is byte-for-byte what the customer would
/// receive — and any input the send would reject fails the preview identically. Carries the
/// same fields as <see cref="SendBookingPassesEmailCommand"/> except the client-generated
/// <c>DispatchId</c> — nothing is recorded, so there is no idempotency key.
/// </summary>
public sealed record PreviewBookingPassesEmailQuery(
    Guid TenantId,
    Guid BookingId,
    string BookingReference,
    BookingPassSheet Sheet,
    IReadOnlyList<PassRecipientInput> Recipients) : IQuery<BookingPassesEmailPreviewResponse>;

/// <summary>
/// The composed preview a dispatcher sees: <paramref name="Subject"/>, <paramref name="HtmlBody"/>
/// and <paramref name="TextBody"/>, plus the distinct validated recipient addresses the passes
/// would go to (echoed for display — nothing is sent). No PDF: the email is the artifact.
/// </summary>
public sealed record BookingPassesEmailPreviewResponse(
    string Subject,
    string HtmlBody,
    string TextBody,
    int RecipientCount,
    IReadOnlyList<string> Recipients);

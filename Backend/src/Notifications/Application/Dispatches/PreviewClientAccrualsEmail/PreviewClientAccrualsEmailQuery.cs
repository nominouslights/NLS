using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Dispatches.SendClientAccrualsEmail;
using NorthernLink.Notifications.Domain;

namespace NorthernLink.Notifications.Application.Dispatches.PreviewClientAccrualsEmail;

/// <summary>
/// Previews the client accruals email without sending anything: it composes the covering
/// email and the report PDF through the exact same <see cref="ClientAccrualsEmailComposer"/>
/// and <see cref="AccrualsRecipientGate"/> the send path uses, so what a dispatcher sees here
/// is byte-for-byte what a contact would receive — and any input the send would reject fails
/// the preview identically. Carries the same fields as
/// <see cref="SendClientAccrualsEmailCommand"/> except the client-generated <c>DispatchId</c>
/// — nothing is recorded, so there is no idempotency key.
/// </summary>
public sealed record PreviewClientAccrualsEmailQuery(
    Guid TenantId,
    Guid ClientId,
    string ClientName,
    NotificationServiceType ServiceType,
    ClientAccrualsReport Report,
    IReadOnlyList<AccrualsRecipientInput> Recipients) : IQuery<AccrualsEmailPreviewResponse>;

/// <summary>
/// The composed preview a dispatcher sees: the covering email's <paramref name="Subject"/>,
/// <paramref name="HtmlBody"/> and <paramref name="TextBody"/>, the report
/// <paramref name="PdfBase64"/> (the same PDF that would be attached, Base64-encoded for
/// transport), and the distinct validated recipient addresses the report would be sent to
/// (echoed for display — nothing is sent).
/// </summary>
public sealed record AccrualsEmailPreviewResponse(
    string Subject,
    string HtmlBody,
    string TextBody,
    string PdfBase64,
    int RecipientCount,
    IReadOnlyList<string> Recipients);

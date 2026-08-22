using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;
using NorthernLink.Notifications.Domain;

namespace NorthernLink.Notifications.Application.Dispatches.PreviewTripPickupReport;

/// <summary>
/// Previews the crew-trip pickup-email report without sending anything: it renders the pickup
/// emails and composes the report artifacts (summary body + PDF) through the exact same
/// <see cref="PickupEmailRenderer"/> / <see cref="PickupEmailReportComposer"/> the send path uses,
/// so what a dispatcher sees here is byte-for-byte what a report recipient would receive. Carries
/// the same fields as <c>SendTripPickupEmailCommand</c> except the client-generated
/// <c>DispatchId</c> — nothing is recorded, so there is no idempotency key. <paramref name="ReportRecipients"/>
/// is echoed back for display only; no report email is dispatched.
/// </summary>
public sealed record PreviewTripPickupReportQuery(
    Guid TenantId,
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
    IReadOnlyList<string> ReportRecipients) : IQuery<PickupReportPreviewResponse>;

/// <summary>
/// The composed preview a dispatcher sees: the report summary email's <paramref name="Subject"/>,
/// <paramref name="HtmlBody"/> and <paramref name="TextBody"/>, the report <paramref name="PdfBase64"/>
/// (the same PDF that would be attached, Base64-encoded for transport), the count of valid pickup
/// recipients, and the distinct valid report recipient addresses the report would be sent to
/// (echoed for display — nothing is sent).
/// </summary>
public sealed record PickupReportPreviewResponse(
    string Subject,
    string HtmlBody,
    string TextBody,
    string PdfBase64,
    int RecipientCount,
    IReadOnlyList<string> ReportRecipients);

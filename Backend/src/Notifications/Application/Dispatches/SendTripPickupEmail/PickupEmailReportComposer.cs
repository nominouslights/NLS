using System.Net;
using System.Text;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Rendering;

namespace NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;

/// <summary>
/// Composes the pickup-email report exactly once for both the send path and the preview: the
/// report PDF, the summary subject/HTML/text body, all from a single set of inputs. Sharing this
/// with <see cref="PickupEmailRenderer"/> is what guarantees a preview shows byte-for-byte what a
/// report recipient would actually receive. The caller supplies the per-recipient items (with the
/// status appropriate to its path — "Sent"/"Failed" on send, "Preview" on preview); the composer
/// only assembles the report artifacts.
/// </summary>
public static class PickupEmailReportComposer
{
    public static PickupReportComposition Compose(PickupReportContext context, IPickupEmailReportPdf reportPdf)
    {
        var report = new PickupEmailReport(
            context.TripNumber,
            context.Route,
            context.TripDate,
            context.TemplateName,
            context.SentAtUtc,
            context.SentCount,
            context.FailedCount,
            context.Items);

        var pdfBytes = reportPdf.Build(report);
        var subject = $"Pickup email report — {context.TripNumber} — {context.TripDate}";
        var htmlBody = BuildReportHtmlBody(context.Route, context.TripDate, context.Items, context.SentCount);
        var textBody = MergeFieldRenderer.RenderText(htmlBody);

        return new PickupReportComposition(subject, htmlBody, textBody, pdfBytes, context.Items);
    }

    private static string BuildReportHtmlBody(
        string route,
        string tripDate,
        IReadOnlyList<PickupEmailReportItem> items,
        int sentCount)
    {
        var recipientList = string.Join(
            ", ",
            items.Select(item =>
                $"{WebUtility.HtmlEncode(item.PassengerName)} &lt;{WebUtility.HtmlEncode(item.Email)}&gt; ({WebUtility.HtmlEncode(item.Status)})"));

        var builder = new StringBuilder();
        builder.Append("<p>");
        builder.Append($"{sentCount} pickup email{(sentCount == 1 ? string.Empty : "s")} sent for ");
        builder.Append($"{WebUtility.HtmlEncode(route)} on {WebUtility.HtmlEncode(tripDate)}.");
        builder.Append("</p>");
        builder.Append($"<p>Sent to: {recipientList}.</p>");
        builder.Append("<p>A PDF copy of each email is attached.</p>");
        return builder.ToString();
    }
}

/// <summary>
/// Everything the composer needs to assemble one report: the trip header context, the send
/// timestamp, the sent/failed tallies for the header, and the already-built per-recipient items.
/// </summary>
public sealed record PickupReportContext(
    string TripNumber,
    string Route,
    string TripDate,
    string TemplateName,
    DateTimeOffset SentAtUtc,
    int SentCount,
    int FailedCount,
    IReadOnlyList<PickupEmailReportItem> Items);

/// <summary>
/// The composed report artifacts: the summary email's subject and bodies, the PDF bytes (to be
/// attached on send or Base64-echoed in a preview), and the items echoed back for convenience.
/// </summary>
public sealed record PickupReportComposition(
    string Subject,
    string HtmlBody,
    string TextBody,
    byte[] PdfBytes,
    IReadOnlyList<PickupEmailReportItem> Items);

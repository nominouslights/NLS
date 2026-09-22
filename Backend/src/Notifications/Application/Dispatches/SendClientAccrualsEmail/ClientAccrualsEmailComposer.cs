using System.Net;
using System.Text;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Rendering;

namespace NorthernLink.Notifications.Application.Dispatches.SendClientAccrualsEmail;

/// <summary>
/// Composes the client accruals email exactly once for both the send path and the preview:
/// the report PDF, the covering subject/HTML/text body, all from the single flat
/// <see cref="ClientAccrualsReport"/>. Sharing this composer is what guarantees a preview
/// shows byte-for-byte what a contact would actually receive — the same contract
/// <see cref="PickupEmailReportComposer"/> holds for the pickup report.
/// </summary>
public static class ClientAccrualsEmailComposer
{
    public static AccrualsEmailComposition Compose(ClientAccrualsReport report, IClientAccrualsReportPdf reportPdf)
    {
        var pdfBytes = reportPdf.Build(report);
        var subject = $"Accruals report — {report.ClientName} — {report.PeriodLabel}";
        var htmlBody = BuildHtmlBody(report);
        var textBody = MergeFieldRenderer.RenderText(htmlBody);

        return new AccrualsEmailComposition(subject, htmlBody, textBody, pdfBytes);
    }

    /// <summary>
    /// File name for the attached PDF, derived from the period label (e.g.
    /// <c>accruals-report-august-2026.pdf</c>) so a contact's downloads folder stays legible.
    /// </summary>
    public static string AttachmentName(ClientAccrualsReport report)
    {
        var slug = Slug(report.PeriodLabel);
        return slug.Length == 0 ? "accruals-report.pdf" : $"accruals-report-{slug}.pdf";
    }

    /// <summary>
    /// The covering note, mirroring the report it attaches: upcoming expenses and the amount
    /// owing lead, settled work follows. The headline figures are echoed verbatim when the
    /// report carries them (they are pre-formatted CAD strings — nothing is computed here, and
    /// no tax appears anywhere, since QuickBooks owns tax); a report with no headline block
    /// falls back to naming the two leading sections without figures. Every interpolated value
    /// goes through <see cref="WebUtility.HtmlEncode"/> — the client name, the period and the
    /// figures are all frontend-supplied strings.
    /// </summary>
    private static string BuildHtmlBody(ClientAccrualsReport report)
    {
        var builder = new StringBuilder();
        builder.Append("<p>");
        builder.Append($"Please find attached the accruals report for {WebUtility.HtmlEncode(report.ClientName)}, ");
        builder.Append($"covering {WebUtility.HtmlEncode(report.PeriodLabel)} (prepared {WebUtility.HtmlEncode(report.PreparedDate)}).");
        builder.Append("</p>");

        builder.Append("<p>It leads with the work still to come and what is owing on the period, ");
        builder.Append("with settled work summarised behind them:</p>");

        if (report.Headline.Count > 0)
        {
            builder.Append("<ul>");
            foreach (var figure in report.Headline)
            {
                builder.Append("<li>");
                builder.Append($"<strong>{WebUtility.HtmlEncode(figure.Label)}:</strong> ");
                builder.Append(WebUtility.HtmlEncode(figure.AmountCad));
                if (figure.Detail.Length > 0)
                {
                    builder.Append($" — {WebUtility.HtmlEncode(figure.Detail)}");
                }

                builder.Append("</li>");
            }

            builder.Append("</ul>");
        }
        else
        {
            builder.Append("<ul><li>Upcoming expenses — work scheduled but not yet done.</li>");
            builder.Append("<li>Monies owed — work invoiced or ready for billing.</li></ul>");
        }

        builder.Append("<p>Estimated amounts are marked and are not invoices; ");
        builder.Append("issued invoices remain the system of record for amounts owing.</p>");
        return builder.ToString();
    }

    // Lowercased letters/digits with single dashes between runs — a safe attachment-name slug.
    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}

/// <summary>
/// The composed email artifacts: the covering email's subject and bodies, and the report PDF
/// bytes (attached on send, Base64-echoed in a preview).
/// </summary>
public sealed record AccrualsEmailComposition(
    string Subject,
    string HtmlBody,
    string TextBody,
    byte[] PdfBytes);

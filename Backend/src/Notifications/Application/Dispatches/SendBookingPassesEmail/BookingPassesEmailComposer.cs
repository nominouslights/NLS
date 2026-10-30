using System.Net;
using System.Text;
using NorthernLink.Notifications.Application.Rendering;

namespace NorthernLink.Notifications.Application.Dispatches.SendBookingPassesEmail;

/// <summary>
/// Composes the booking-passes email exactly once for both the send path and the preview:
/// subject, an HTML body with the booking summary and one bordered pass block per traveller,
/// and the plain-text fallback — all from the single flat <see cref="BookingPassSheet"/>.
/// No PDF: the pass IS the email (the dispatcher's print path is the browser). Every sheet
/// field is HTML-encoded on the way in — traveller names, notes and addresses are customer
/// input. Sharing this composer is what guarantees a preview shows byte-for-byte what the
/// customer receives, the same contract <see cref="SendClientAccrualsEmail.ClientAccrualsEmailComposer"/> holds.
/// </summary>
public static class BookingPassesEmailComposer
{
    /// <summary>Marker attribute on each pass block — one per traveller, countable in tests and CSS-hookable.</summary>
    public const string PassBlockClass = "nl-pass";

    public static BookingPassesEmailComposition Compose(BookingPassSheet sheet)
    {
        var subject = $"Your Northern Link passes — {sheet.Reference} — {sheet.CorridorName} — {sheet.ServiceDate}";
        var htmlBody = BuildHtmlBody(sheet);
        var textBody = MergeFieldRenderer.RenderText(htmlBody);

        return new BookingPassesEmailComposition(subject, htmlBody, textBody);
    }

    private static string BuildHtmlBody(BookingPassSheet sheet)
    {
        var builder = new StringBuilder();
        var travellerCount = sheet.Travellers.Count;
        var passWord = travellerCount == 1 ? "pass" : "passes";

        builder.Append($"<p>Hello {Encode(sheet.CustomerName)},</p>");
        builder.Append($"<p>Here {(travellerCount == 1 ? "is" : "are")} your {travellerCount} boarding {passWord} for booking ");
        builder.Append($"<strong>{Encode(sheet.Reference)}</strong> on {Encode(sheet.ServiceDate)}. ");
        builder.Append("Each traveller has their own pass below — show it at boarding, on a phone or printed.</p>");

        // Booking summary.
        builder.Append("<table cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse;margin:12px 0\">");
        AppendRow(builder, "Booking reference", sheet.Reference);
        AppendRow(builder, "Service date", sheet.ServiceDate);
        AppendRow(builder, "Corridor", sheet.CorridorName);
        AppendRow(builder, "Pickup", sheet.Pickup);
        AppendRow(builder, "Drop-off", sheet.Dropoff);
        AppendRow(builder, "Travellers", travellerCount.ToString());
        AppendRow(builder, "Payment method", sheet.PaymentMethod);
        AppendRow(builder, "Payment status", sheet.PaymentStatus);
        builder.Append("</table>");

        // One bordered pass per traveller.
        foreach (var traveller in sheet.Travellers)
        {
            builder.Append($"<div class=\"{PassBlockClass}\" style=\"border:2px solid #1F2933;border-radius:6px;padding:14px 16px;margin:16px 0\">");
            builder.Append($"<p style=\"margin:0 0 6px 0;font-size:12px;letter-spacing:1px;text-transform:uppercase\">Northern Link Shuttle &amp; Cargo — Boarding pass {Encode(traveller.Seat)}</p>");
            builder.Append($"<p style=\"margin:0 0 10px 0;font-size:20px\"><strong>{Encode(traveller.Name)}</strong></p>");
            builder.Append("<table cellpadding=\"3\" cellspacing=\"0\" style=\"border-collapse:collapse\">");
            AppendRow(builder, "Reference", sheet.Reference);
            AppendRow(builder, "Date", sheet.ServiceDate);
            AppendRow(builder, "Corridor", sheet.CorridorName);
            AppendRow(builder, "Route", $"{sheet.Pickup} → {sheet.Dropoff}");
            if (!string.IsNullOrWhiteSpace(traveller.Phone))
            {
                AppendRow(builder, "Phone", traveller.Phone);
            }

            builder.Append("</table>");
            builder.Append("<p style=\"margin:10px 0 0 0\"><em>Present this pass at boarding.</em></p>");
            builder.Append("</div>");
        }

        if (!string.IsNullOrWhiteSpace(sheet.Notes))
        {
            builder.Append($"<p><strong>Notes:</strong> {Encode(sheet.Notes)}</p>");
        }

        builder.Append("<p>Please arrive at your pickup a few minutes early. ");
        builder.Append("If anything on these passes is wrong, reply to this email or call dispatch quoting your booking reference.</p>");
        builder.Append("<p>Safe travels,<br/>Northern Link Shuttle &amp; Cargo</p>");

        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, string label, string value)
    {
        builder.Append("<tr>");
        builder.Append($"<td style=\"padding-right:12px;color:#52606D\">{Encode(label)}</td>");
        builder.Append($"<td>{Encode(value)}</td>");
        builder.Append("</tr>");
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}

/// <summary>The composed email artifacts: subject, HTML body and plain-text body. No attachment.</summary>
public sealed record BookingPassesEmailComposition(
    string Subject,
    string HtmlBody,
    string TextBody);

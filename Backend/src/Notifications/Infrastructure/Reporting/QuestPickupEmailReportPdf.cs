using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Dispatches;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NorthernLink.Notifications.Infrastructure.Reporting;

/// <summary>
/// <see cref="IPickupEmailReportPdf"/> backed by QuestPDF (Community license, set once at DI
/// time in <c>AddNotifications</c>). Renders a single-document report: a header block with the
/// trip context and the sent/failed tally, then one block per recipient carrying the passenger,
/// the send status (color + text label, per the platform's status-indicator rule), the exact
/// subject, and the plain-text body that recipient received. Robust to empty body text.
/// </summary>
public sealed class QuestPickupEmailReportPdf : IPickupEmailReportPdf
{
    // Status colors from the platform's colorblind-safe palette, always paired with a text label.
    private const string SentColor = "#009E73";   // Teal — sent.
    private const string FailedColor = "#D55E00"; // Vermillion — failed.
    private const string MutedColor = "#4A4A4A";  // Neutral gray — secondary text.

    public byte[] Build(PickupEmailReport report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(header => ComposeHeader(header, report));
                page.Content().Element(content => ComposeContent(content, report));
                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Page ").FontSize(8).FontColor(MutedColor);
                    text.CurrentPageNumber().FontSize(8).FontColor(MutedColor);
                    text.Span(" of ").FontSize(8).FontColor(MutedColor);
                    text.TotalPages().FontSize(8).FontColor(MutedColor);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, PickupEmailReport report)
    {
        container.Column(column =>
        {
            column.Item().Text($"Pickup Email Report — {report.TripNumber}")
                .FontSize(16).Bold();

            column.Item().PaddingTop(2).Text(report.Route).FontSize(11).FontColor(MutedColor);

            column.Item().PaddingTop(6).Text(text =>
            {
                text.Span("Trip date: ").SemiBold();
                text.Span(report.TripDate);
                text.Span("     Template: ").SemiBold();
                text.Span(report.TemplateName);
            });

            column.Item().PaddingTop(2).Text(text =>
            {
                text.Span($"{report.SentCount} sent").FontColor(SentColor).SemiBold();
                text.Span(" / ");
                text.Span($"{report.FailedCount} failed").FontColor(FailedColor).SemiBold();
            });

            column.Item().PaddingTop(2).Text(
                $"Generated {report.SentAtUtc:yyyy-MM-dd HH:mm} UTC")
                .FontSize(8).FontColor(MutedColor);

            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(IContainer container, PickupEmailReport report)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Spacing(12);

            foreach (var item in report.Items)
            {
                column.Item().Element(block => ComposeItem(block, item));
            }
        });
    }

    private static void ComposeItem(IContainer container, PickupEmailReportItem item)
    {
        var isSent = string.Equals(item.Status, "Sent", StringComparison.OrdinalIgnoreCase);
        var statusColor = isSent ? SentColor : FailedColor;
        var bodyText = string.IsNullOrWhiteSpace(item.BodyText) ? "(no body content)" : item.BodyText;

        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(10)
            .Column(column =>
            {
                column.Item().Text(text =>
                {
                    text.Span(item.PassengerName).SemiBold();
                    text.Span($"  <{item.Email}>").FontColor(MutedColor);
                });

                column.Item().PaddingTop(2).Text(text =>
                {
                    text.Span("Status: ").FontColor(MutedColor);
                    text.Span(item.Status).FontColor(statusColor).SemiBold();
                });

                column.Item().PaddingTop(6).Text(item.Subject).Bold();

                column.Item().PaddingTop(4).Text(bodyText).FontColor(MutedColor);
            });
    }
}

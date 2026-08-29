using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Dispatches;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NorthernLink.Notifications.Infrastructure.Reporting;

/// <summary>
/// <see cref="IClientAccrualsReportPdf"/> backed by QuestPDF (Community license, set once at
/// DI time in <c>AddNotifications</c>). Renders a single-document report: a header block with
/// the client and period, any notes, the bucket summary table, one detail table per bucket,
/// the reconciliation section, and the referenced invoices (the only place GST appears).
/// Every value is an already-formatted string from the flat <see cref="ClientAccrualsReport"/>
/// — no domain lookups, and empty sections are simply skipped (an empty month still renders).
/// </summary>
public sealed class QuestClientAccrualsReportPdf : IClientAccrualsReportPdf
{
    // Neutral gray from the platform's colorblind-safe palette — secondary text only; the
    // report carries no status semantics, so no status colors appear.
    private const string MutedColor = "#4A4A4A";

    public byte[] Build(ClientAccrualsReport report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(9));

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

    private static void ComposeHeader(IContainer container, ClientAccrualsReport report)
    {
        container.Column(column =>
        {
            column.Item().Text($"Accruals Report — {report.ClientName}")
                .FontSize(16).Bold();

            column.Item().PaddingTop(2).Text(report.PeriodLabel).FontSize(11).FontColor(MutedColor);

            column.Item().PaddingTop(6).Text(text =>
            {
                text.Span("Prepared: ").SemiBold();
                text.Span(report.PreparedDate);
            });

            column.Item().PaddingTop(2).Text(
                "Estimated amounts are marked and are not invoices. All amounts CAD; GST appears only in the invoices section.")
                .FontSize(8).FontColor(MutedColor);

            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(IContainer container, ClientAccrualsReport report)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Spacing(14);

            if (report.Notes.Count > 0)
            {
                column.Item().Column(notes =>
                {
                    notes.Spacing(2);
                    foreach (var note in report.Notes)
                    {
                        notes.Item().Text(note).FontSize(8).FontColor(MutedColor);
                    }
                });
            }

            if (report.Summary.Count > 0)
            {
                column.Item().Element(block => ComposeSummary(block, report.Summary));
            }

            foreach (var bucket in report.Buckets)
            {
                column.Item().Element(block => ComposeBucket(block, bucket));
            }

            if (report.Reconciliation.Count > 0)
            {
                column.Item().Element(block => ComposeReconciliation(block, report.Reconciliation));
            }

            if (report.Invoices.Count > 0)
            {
                column.Item().Element(block => ComposeInvoices(block, report.Invoices));
            }
        });
    }

    private static void ComposeSummary(IContainer container, IReadOnlyList<AccrualsSummaryRow> summary)
    {
        container.Column(column =>
        {
            column.Item().Text("Summary").FontSize(11).SemiBold();

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Bucket");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Round trips");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Actual");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Estimated");
                });

                foreach (var row in summary)
                {
                    table.Cell().Element(BodyCell).Text(row.BucketLabel);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.RoundTrips);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.ActualCad);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.EstimatedCad);
                }
            });
        });
    }

    private static void ComposeBucket(IContainer container, AccrualsReportBucket bucket)
    {
        container.Column(column =>
        {
            column.Item().Text(bucket.Label).FontSize(11).SemiBold();

            if (bucket.Rows.Count == 0)
            {
                column.Item().PaddingTop(2).Text("No trips.").FontSize(8).FontColor(MutedColor);
                return;
            }

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Date");
                    header.Cell().Element(HeaderCell).Text("Trip #s");
                    header.Cell().Element(HeaderCell).Text("Route");
                    header.Cell().Element(HeaderCell).Text("PO");
                    header.Cell().Element(HeaderCell).Text("Ref");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Amount");
                });

                foreach (var row in bucket.Rows)
                {
                    table.Cell().Element(BodyCell).Text(row.Date);
                    table.Cell().Element(BodyCell).Text(row.TripNumbers);
                    table.Cell().Element(BodyCell).Text(row.Route);
                    table.Cell().Element(BodyCell).Text(row.PoNumber);
                    table.Cell().Element(BodyCell).Text(row.Reference);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.AmountCad);
                }
            });
        });
    }

    private static void ComposeReconciliation(IContainer container, IReadOnlyList<AccrualsReconciliationRow> rows)
    {
        container.Column(column =>
        {
            column.Item().Text("Reconciliation").FontSize(11).SemiBold();

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Date");
                    header.Cell().Element(HeaderCell).Text("Trip #s");
                    header.Cell().Element(HeaderCell).Text("Route");
                    header.Cell().Element(HeaderCell).Text("Status");
                    header.Cell().Element(HeaderCell).Text("Reason");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Amount");
                });

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).Text(row.Date);
                    table.Cell().Element(BodyCell).Text(row.TripNumbers);
                    table.Cell().Element(BodyCell).Text(row.Route);
                    table.Cell().Element(BodyCell).Text(row.Status);
                    table.Cell().Element(BodyCell).Text(row.Reason);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.AmountCad);
                }
            });
        });
    }

    private static void ComposeInvoices(IContainer container, IReadOnlyList<AccrualsInvoiceRow> invoices)
    {
        container.Column(column =>
        {
            column.Item().Text("Invoices referenced").FontSize(11).SemiBold();

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Invoice #");
                    header.Cell().Element(HeaderCell).Text("Status");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Subtotal");
                    header.Cell().Element(HeaderCell).AlignRight().Text("GST");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                });

                foreach (var invoice in invoices)
                {
                    table.Cell().Element(BodyCell).Text(invoice.InvoiceNumber);
                    table.Cell().Element(BodyCell).Text(invoice.Status);
                    table.Cell().Element(BodyCell).AlignRight().Text(invoice.SubtotalCad);
                    table.Cell().Element(BodyCell).AlignRight().Text(invoice.GstCad);
                    table.Cell().Element(BodyCell).AlignRight().Text(invoice.TotalCad);
                }
            });
        });
    }

    private static IContainer HeaderCell(IContainer container) => container
        .BorderBottom(1)
        .BorderColor(Colors.Grey.Lighten1)
        .PaddingVertical(3)
        .PaddingHorizontal(2)
        .DefaultTextStyle(x => x.SemiBold().FontSize(8));

    private static IContainer BodyCell(IContainer container) => container
        .BorderBottom(1)
        .BorderColor(Colors.Grey.Lighten3)
        .PaddingVertical(3)
        .PaddingHorizontal(2);
}

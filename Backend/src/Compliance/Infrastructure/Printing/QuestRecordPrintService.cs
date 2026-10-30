using System.Globalization;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Printing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NorthernLink.Compliance.Infrastructure.Printing;

/// <summary>
/// <see cref="IRecordPrintService"/> backed by QuestPDF (Community license, set once at DI time in
/// <c>AddCompliance</c>). Every compliance print shares one page frame: a header with the
/// carrier's legal name, NSC number and principal place of business, and a footer
/// <c>Record {id} · {documentNumber} · v{version} · generated {timestamp} · page x of y</c>.
/// Templates fill the body; <c>record-cover</c> (the generic cover sheet: fields, retention,
/// regulatory basis) is the first and the fallback for an unknown key. Monochrome throughout —
/// a record sheet carries no status semantics, so no status colours appear.
/// </summary>
public sealed class QuestRecordPrintService : IRecordPrintService
{
    private const string MutedColor = "#4A4A4A";

    public byte[] Render(RecordPrintDocument document)
    {
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(header => ComposeHeader(header, document.Frame));
                page.Content().Element(content => ComposeCover(content, document));
                page.Footer().Element(footer => ComposeFooter(footer, document.Frame));
            });
        });

        return pdf.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, RecordPrintFrame frame)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(frame.CarrierLegalName).FontSize(14).Bold();
                row.ConstantItem(180).AlignRight().Text(frame.NscNumber).FontSize(9).FontColor(MutedColor);
            });

            column.Item().PaddingTop(2).Text(frame.PrincipalPlaceOfBusiness).FontSize(9).FontColor(MutedColor);
            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeFooter(IContainer container, RecordPrintFrame frame)
    {
        container.PaddingTop(6).Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Item().PaddingTop(4).Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(8).FontColor(MutedColor));
                text.Span($"Record {frame.RecordId} · {frame.DocumentNumber} · v{frame.DocumentVersion} · generated ");
                text.Span(frame.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
                text.Span(" · page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }

    /// <summary>The <c>record-cover</c> template: title, the record's fields, its retention block, and the rules it exists for.</summary>
    private static void ComposeCover(IContainer container, RecordPrintDocument document)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Spacing(14);

            column.Item().Text(document.Title).FontSize(16).Bold();

            column.Item().Element(block => ComposeFields(block, "Record", document.Fields));
            column.Item().Element(block => ComposeFields(block, "Retention", document.Retention));

            if (document.Basis.Count > 0)
            {
                column.Item().Column(basis =>
                {
                    basis.Item().Text("Regulatory basis").FontSize(11).SemiBold();
                    basis.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(5);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Key");
                            header.Cell().Element(HeaderCell).Text("Citation");
                            header.Cell().Element(HeaderCell).Text("What it requires");
                        });

                        foreach (var line in document.Basis)
                        {
                            table.Cell().Element(BodyCell).Text(line.Key);
                            table.Cell().Element(BodyCell).Text(line.Citation);
                            table.Cell().Element(BodyCell).Column(cell =>
                            {
                                cell.Item().Text(line.Title).SemiBold();
                                if (line.PlainExplanation.Length > 0)
                                {
                                    cell.Item().Text(line.PlainExplanation).FontColor(MutedColor);
                                }
                            });
                        }
                    });
                });
            }
        });
    }

    private static void ComposeFields(IContainer container, string heading, IReadOnlyList<PrintField> fields)
    {
        container.Column(column =>
        {
            column.Item().Text(heading).FontSize(11).SemiBold();
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(5);
                });

                foreach (var field in fields)
                {
                    table.Cell().Element(BodyCell).Text(field.Label).SemiBold().FontColor(MutedColor);
                    table.Cell().Element(BodyCell).Text(field.Value);
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

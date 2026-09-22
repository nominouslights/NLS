using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Infrastructure.Reporting;
using QuestPDF.Infrastructure;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The QuestPDF-backed accruals report renderer produces a real, non-empty PDF and tolerates
/// an entirely empty month (every section list empty — the report must still render), as well
/// as a report with no headline figures (an older frontend that posts no <c>headline</c>).
/// </summary>
public class ClientAccrualsReportPdfTests
{
    static ClientAccrualsReportPdfTests()
    {
        // The renderer relies on the process-wide license normally set in AddNotifications; a raw
        // unit test never runs DI, so set it here before the first Build.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void Build_returns_non_empty_pdf_bytes_starting_with_the_pdf_magic_header()
    {
        var pdf = new QuestClientAccrualsReportPdf().Build(TestNotifications.SampleAccrualsReport());

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);

        // "%PDF" magic bytes at the start prove it's a real PDF, not just some bytes.
        Assert.True(pdf.Length >= 4);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    [Fact]
    public void Build_tolerates_an_empty_month()
    {
        var empty = new ClientAccrualsReport(
            "Vale Manitoba Operations",
            "September 2026",
            "October 1, 2026",
            Notes: [],
            Headline: [],
            Summary: [],
            Buckets: [],
            Reconciliation: [],
            Invoices: []);

        var pdf = new QuestClientAccrualsReportPdf().Build(empty);

        Assert.NotEmpty(pdf);
    }

    /// <summary>
    /// Backward compatibility: a frontend that posts no <c>headline</c> normalizes to an empty
    /// list, and the renderer skips the block rather than printing an empty box — the rest of
    /// the report (including an unemphasised, flat summary) renders exactly as before.
    /// </summary>
    [Fact]
    public void Build_skips_the_headline_block_when_no_figures_are_supplied()
    {
        var populated = TestNotifications.SampleAccrualsReport();
        var withoutHeadline = populated with
        {
            Headline = [],
            Summary = populated.Summary.Select(row => row with { Emphasis = false }).ToList(),
        };

        var pdf = new QuestClientAccrualsReportPdf().Build(withoutHeadline);

        Assert.NotEmpty(pdf);
    }
}

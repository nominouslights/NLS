using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Infrastructure.Reporting;
using QuestPDF.Infrastructure;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The QuestPDF-backed pickup-email report renderer produces a real, non-empty PDF and tolerates
/// an item with empty body text (the "(no body content)" fallback path).
/// </summary>
public class PickupEmailReportPdfTests
{
    static PickupEmailReportPdfTests()
    {
        // The renderer relies on the process-wide license normally set in AddNotifications; a raw
        // unit test never runs DI, so set it here before the first Build.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static PickupEmailReport SampleReport() => new(
        "NL-1042",
        "Thompson – Lynn Lake",
        "Tuesday, August 4, 2026",
        "Crew pickup",
        DateTimeOffset.UtcNow,
        SentCount: 1,
        FailedCount: 1,
        Items:
        [
            new PickupEmailReportItem(
                "Alex Moody", "alex@example.com", "Sent",
                "Pickup Tuesday, August 4, 2026 — NL-1042",
                "Hi Alex Moody, pickup at Thompson Terminal at 8:30 AM."),
            // An item with empty body text — the renderer must not throw on it.
            new PickupEmailReportItem(
                "Jordan Bell", "jordan@example.com", "Failed",
                "Pickup Tuesday, August 4, 2026 — NL-1042",
                string.Empty),
        ]);

    [Fact]
    public void Build_returns_non_empty_pdf_bytes_starting_with_the_pdf_magic_header()
    {
        var pdf = new QuestPickupEmailReportPdf().Build(SampleReport());

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);

        // "%PDF" magic bytes at the start prove it's a real PDF, not just some bytes.
        Assert.True(pdf.Length >= 4);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }
}

using NorthernLink.Compliance.Application.Records.Print;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Compliance.Infrastructure.Printing;
using QuestPDF.Infrastructure;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>The QuestPDF record print frame renders a real PDF for the cover-sheet template.</summary>
public class RecordPrintServiceTests
{
    static RecordPrintServiceTests()
    {
        // Set once in AddCompliance at runtime; a raw unit test never runs DI.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly RegulatoryBasis Basis = RegulatoryBasis.Create(new RegulatoryBasisDefinition(
        BasisKeys.Nsc13_16, "CA", RegulatoryInstrument.NSC, "NSC 13 s. 16", "Trip Inspection — retention", null,
        "The company keeps the original of every inspection report for at least six months.", null, null, null, new DateOnly(2026, 10, 1), 1));

    [Fact]
    public void Build_returns_non_empty_pdf_bytes_starting_with_the_pdf_magic_header()
    {
        var record = TestCompliance.Record();
        var settings = TenantComplianceSettings.CreateDefault(TestCompliance.TenantId, null);
        var document = GetRecordPrintQueryHandler.BuildDocument(
            record, ComplianceRecordCatalogue.For(record.RecordType), settings, [Basis], DateTimeOffset.UtcNow);

        var pdf = new QuestRecordPrintService().Render(document);

        Assert.NotEmpty(pdf);
        Assert.True(pdf.Length >= 4);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    [Fact]
    public void The_frame_carries_the_carrier_the_record_and_the_retention_block()
    {
        var record = TestCompliance.Record();
        var document = GetRecordPrintQueryHandler.BuildDocument(
            record, ComplianceRecordCatalogue.For(record.RecordType), null, [], new DateTimeOffset(2026, 10, 11, 14, 0, 0, TimeSpan.Zero));

        Assert.Equal("Northern Link Shuttle & Cargo", document.Frame.CarrierLegalName);
        Assert.Equal(record.DocumentNumber, document.Frame.DocumentNumber);
        Assert.Equal(1, document.Frame.DocumentVersion);
        Assert.Equal("record-cover", document.TemplateKey);
        Assert.Contains(document.Retention, f => f.Label == "Retain until" && f.Value == "2027-03-30");
        Assert.Contains(document.Fields, f => f.Label == "Record type" && f.Value.StartsWith("R9"));
        // A key the catalogue has not been seeded with still prints, flagged, rather than failing the sheet.
        Assert.All(document.Basis, line => Assert.Equal("Not in catalogue", line.Title));
    }

    [Fact]
    public void A_pending_and_a_permanent_record_both_render()
    {
        var pending = TestCompliance.Record(ComplianceRecordType.R19, TestCompliance.Registration(
            sourceModule: "incidents", sourceRecordId: "inc:1",
            subject: new RecordSubject(RecordSubjectKind.Incident, Guid.NewGuid(), "Collision"),
            anchors: TestCompliance.Anchors((AnchorKind.RecordDate, new DateOnly(2026, 1, 10)))), "NL-IR-01-2026-0001");
        var permanent = TestCompliance.Record(ComplianceRecordType.R21, TestCompliance.Registration(
            sourceModule: "compliance", sourceRecordId: "plan:1",
            subject: new RecordSubject(RecordSubjectKind.Carrier, null, "Northern Link Shuttle & Cargo")), "NL-SP-02-2026-0001");

        foreach (var record in new[] { pending, permanent })
        {
            var document = GetRecordPrintQueryHandler.BuildDocument(record, ComplianceRecordCatalogue.For(record.RecordType), null, [], DateTimeOffset.UtcNow);
            Assert.NotEmpty(new QuestRecordPrintService().Render(document));
        }
    }
}

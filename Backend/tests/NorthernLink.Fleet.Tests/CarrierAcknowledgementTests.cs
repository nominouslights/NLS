using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The NL-PTI-01 carrier acknowledgement line: evidence that a report carrying a Major or
/// OutOfService defect was escalated to the carrier and seen. Its value is entirely in what it
/// CANNOT say — so the rejections below are the real subject of this file, not the happy path.
/// A stamp that could be applied to a clean report, applied twice, applied anonymously, or
/// applied by the same caller that filed the report would evidence nothing at all.
/// </summary>
public class CarrierAcknowledgementTests
{
    private static readonly DateTimeOffset AcknowledgedAt =
        new(2026, 9, 18, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void A_failed_inspection_can_be_acknowledged_by_the_carrier()
    {
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);
        Assert.Equal(InspectionResult.Fail, inspection.Result);

        var result = inspection.AcknowledgeAsCarrier("R. Beardy", "Unit parked pending repair", AcknowledgedAt);

        Assert.True(result.IsSuccess);
        Assert.Equal("R. Beardy", inspection.CarrierAcknowledgedBy);
        Assert.Equal(AcknowledgedAt, inspection.CarrierAcknowledgedAtUtc);
        Assert.Equal("Unit parked pending repair", inspection.CarrierAcknowledgementNote);
    }

    [Fact]
    public void Acknowledging_raises_a_domain_event_so_the_projection_sees_the_write()
    {
        // Without an event there is no event_journal row, the projector never runs, and
        // rm_vehicle_inspections serves the report as unacknowledged forever.
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.OutOfService)]);

        Assert.True(inspection.AcknowledgeAsCarrier("R. Beardy", note: null, AcknowledgedAt).IsSuccess);

        Assert.Contains(inspection.DomainEvents, e => e is VehicleInspectionCarrierAcknowledgedDomainEvent);
    }

    [Fact]
    public void A_clean_inspection_cannot_be_acknowledged()
    {
        var inspection = TestInspections.PreTrip();
        Assert.Equal(InspectionResult.Pass, inspection.Result);

        var result = inspection.AcknowledgeAsCarrier("R. Beardy", note: null, AcknowledgedAt);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.CarrierAcknowledgementNotRequired, result.Error);
        Assert.Null(inspection.CarrierAcknowledgedBy);
        Assert.Null(inspection.CarrierAcknowledgedAtUtc);
    }

    [Fact]
    public void An_inspection_that_only_passed_with_minor_defects_cannot_be_acknowledged()
    {
        // The line is about escalation of a MAJOR fault. A minor defect is logged, not escalated.
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Minor)]);
        Assert.Equal(InspectionResult.PassWithDefects, inspection.Result);

        var result = inspection.AcknowledgeAsCarrier("R. Beardy", note: null, AcknowledgedAt);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.CarrierAcknowledgementNotRequired, result.Error);
    }

    [Fact]
    public void A_second_acknowledgement_fails_rather_than_re_stamping()
    {
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);
        Assert.True(inspection.AcknowledgeAsCarrier("R. Beardy", "first", AcknowledgedAt).IsSuccess);

        var second = inspection.AcknowledgeAsCarrier(
            "Someone Else", "second", AcknowledgedAt.AddDays(3));

        Assert.True(second.IsFailure);
        Assert.Equal(InspectionErrors.CarrierAlreadyAcknowledged, second.Error);

        // Nothing moved — who signed and when is exactly what must not be overwritable.
        Assert.Equal("R. Beardy", inspection.CarrierAcknowledgedBy);
        Assert.Equal(AcknowledgedAt, inspection.CarrierAcknowledgedAtUtc);
        Assert.Equal("first", inspection.CarrierAcknowledgementNote);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_name_is_rejected(string acknowledgedBy)
    {
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);

        var result = inspection.AcknowledgeAsCarrier(acknowledgedBy, note: null, AcknowledgedAt);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.CarrierAcknowledgerRequired, result.Error);
        Assert.Null(inspection.CarrierAcknowledgedAtUtc);
    }

    [Fact]
    public void The_name_and_note_are_trimmed_like_the_other_name_fields()
    {
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);

        Assert.True(inspection.AcknowledgeAsCarrier("  R. Beardy  ", "  seen  ", AcknowledgedAt).IsSuccess);

        Assert.Equal("R. Beardy", inspection.CarrierAcknowledgedBy);
        Assert.Equal("seen", inspection.CarrierAcknowledgementNote);
    }

    [Fact]
    public void A_whitespace_note_is_stored_as_null_rather_than_blank()
    {
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);

        Assert.True(inspection.AcknowledgeAsCarrier("R. Beardy", "   ", AcknowledgedAt).IsSuccess);

        Assert.Null(inspection.CarrierAcknowledgementNote);
    }

    [Fact]
    public void Entering_an_inspection_cannot_set_the_acknowledgement()
    {
        // The back door, half one. Enter takes no acknowledgement parameters at all — which is
        // the guard — so this pins that a freshly entered failed report starts unacknowledged
        // and that the only way to change that is AcknowledgeAsCarrier.
        var inspection = TestInspections.PostTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);

        Assert.Equal(InspectionResult.Fail, inspection.Result);
        Assert.Null(inspection.CarrierAcknowledgedBy);
        Assert.Null(inspection.CarrierAcknowledgedAtUtc);
        Assert.Null(inspection.CarrierAcknowledgementNote);
    }

    [Fact]
    public void Amending_an_inspection_cannot_set_the_acknowledgement()
    {
        // The back door, half two, and the one that matters: PUT /api/fleet/inspections/{id} is
        // a dispatcher-writable path. If an amend could carry a carrier stamp, the caller filing
        // the report could sign the carrier's line on it, which is the exact escalation the
        // field exists to evidence NOT happening.
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);

        var result = TestInspections.AmendWith(
            inspection,
            [TestInspections.Defect(InspectionDefectSeverity.Major)],
            odometerKm: 118_999);

        Assert.True(result.IsSuccess);
        Assert.Null(inspection.CarrierAcknowledgedBy);
        Assert.Null(inspection.CarrierAcknowledgedAtUtc);
        Assert.Null(inspection.CarrierAcknowledgementNote);
    }

    [Fact]
    public void An_amendment_that_downgrades_a_failed_report_leaves_the_acknowledgement_in_place()
    {
        // The documented ordering decision. Result is re-derived by every amend, so an
        // acknowledged Fail can become a PassWithDefects afterwards. The stamp STAYS: it records
        // what was escalated at the time, and clearing it would destroy the only evidence that
        // the carrier ever saw the defect that has since been downgraded. Pinned here so
        // reversing it later is a deliberate decision rather than an accident.
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);
        Assert.True(inspection.AcknowledgeAsCarrier("R. Beardy", "seen", AcknowledgedAt).IsSuccess);

        var result = TestInspections.AmendWith(
            inspection, [TestInspections.Defect(InspectionDefectSeverity.Minor)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(InspectionResult.PassWithDefects, inspection.Result);
        Assert.Equal("R. Beardy", inspection.CarrierAcknowledgedBy);
        Assert.Equal(AcknowledgedAt, inspection.CarrierAcknowledgedAtUtc);
    }

    [Fact]
    public void The_certification_statement_is_stored_verbatim_on_entry()
    {
        const string Statement =
            "I certify that I have inspected this vehicle in accordance with NSC Standard 13 "
            + "and that the defects recorded above are the only defects found.";

        var inspection = TestInspections.PostTrip(certificationStatement: Statement);

        Assert.Equal(Statement, inspection.CertificationStatement);
    }
}

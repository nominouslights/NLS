using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The amend trap. <see cref="VehicleInspection.Amend"/> used to end with a wholesale
/// <c>Defects = [.. defects]</c>, and the incoming list comes off the
/// <c>PUT /api/fleet/inspections/{id}</c> body, whose defect shape carries no resolution fields.
/// Left alone, correcting an odometer typo would silently un-resolve every defect on the
/// inspection: faults already repaired under a completed work order would reappear as open on
/// every dispatcher's screen with their repair evidence erased. It corrupts a safety record, and
/// quietly — so these are the load-bearing regression tests of the whole feature.
/// </summary>
public class AmendInspectionResolutionTests
{
    private static readonly DateTimeOffset ResolvedAt =
        new(2026, 3, 14, 17, 5, 0, TimeSpan.Zero);

    private static InspectionDefect Defect(
        string item,
        InspectionDefectSeverity severity = InspectionDefectSeverity.Major,
        string? note = "as found") =>
        new() { Item = item, Severity = severity, Note = note };

    /// <summary>An inspection with "Brakes" resolved under a work order, and "Defroster" still open.</summary>
    private static (VehicleInspection Inspection, Guid WorkOrderId) WithOneResolvedDefect()
    {
        var inspection = TestInspections.PreTrip(defects: [Defect("Brakes"), Defect("Defroster")]);
        var workOrderId = Guid.NewGuid();

        Assert.True(inspection.LinkWorkOrder(workOrderId).IsSuccess);
        Assert.True(inspection
            .ResolveDefect("Brakes", DefectResolutionReason.RepairedUnderWorkOrder, "new line fitted", "M. Cardinal", ResolvedAt)
            .IsSuccess);

        return (inspection, workOrderId);
    }

    [Fact]
    public void Amending_an_inspection_does_not_un_resolve_its_defects()
    {
        var (inspection, _) = WithOneResolvedDefect();

        // The realistic case: a dispatcher fixes the odometer and re-sends the same defects.
        var result = TestInspections.AmendWith(
            inspection, [Defect("Brakes"), Defect("Defroster")], odometerKm: 118_999);

        Assert.True(result.IsSuccess);

        var brakes = inspection.Defects.Single(d => d.Item == "Brakes");
        Assert.True(brakes.IsResolved);
        Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, brakes.ResolutionReason);
        Assert.Equal("new line fitted", brakes.ResolutionNote);
        Assert.Equal("M. Cardinal", brakes.ResolvedBy);
        Assert.Equal(ResolvedAt, brakes.ResolvedAtUtc);

        Assert.False(inspection.Defects.Single(d => d.Item == "Defroster").IsResolved);
        Assert.Equal(118_999, inspection.OdometerKm);
    }

    [Fact]
    public void Amending_a_resolved_defects_severity_and_note_keeps_the_resolution_and_takes_the_new_values()
    {
        var (inspection, _) = WithOneResolvedDefect();

        var result = TestInspections.AmendWith(inspection, [
            Defect("Brakes", InspectionDefectSeverity.OutOfService, "worse than first written"),
            Defect("Defroster"),
        ]);

        Assert.True(result.IsSuccess);

        var brakes = inspection.Defects.Single(d => d.Item == "Brakes");

        // The amended values win — correcting a severity is the whole point of an amend.
        Assert.Equal(InspectionDefectSeverity.OutOfService, brakes.Severity);
        Assert.Equal("worse than first written", brakes.Note);

        // ...and the stamp was copied onto the NEW record, not kept by holding the old one whole.
        Assert.True(brakes.IsResolved);
        Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, brakes.ResolutionReason);
        Assert.Equal("M. Cardinal", brakes.ResolvedBy);

        // Re-derived from the amended severities, as always.
        Assert.Equal(InspectionResult.Fail, inspection.Result);
    }

    [Fact]
    public void Amending_a_resolved_defect_out_of_the_list_drops_its_resolution_with_it()
    {
        var (inspection, _) = WithOneResolvedDefect();

        var result = TestInspections.AmendWith(inspection, [Defect("Defroster")]);

        Assert.True(result.IsSuccess);

        // The report of the fault is being retracted, so the record of clearing it is meaningless.
        var remaining = Assert.Single(inspection.Defects);
        Assert.Equal("Defroster", remaining.Item);
        Assert.False(remaining.IsResolved);
    }

    [Fact]
    public void A_defect_the_amendment_adds_starts_unresolved()
    {
        var (inspection, _) = WithOneResolvedDefect();

        var result = TestInspections.AmendWith(
            inspection, [Defect("Brakes"), Defect("Defroster"), Defect("Headlights", InspectionDefectSeverity.Minor)]);

        Assert.True(result.IsSuccess);

        var headlights = inspection.Defects.Single(d => d.Item == "Headlights");
        Assert.False(headlights.IsResolved);
        Assert.Null(headlights.ResolutionReason);
        Assert.Null(headlights.ResolvedBy);
        Assert.Null(headlights.ResolvedAtUtc);
        Assert.Null(headlights.ResolvedByWorkOrderId);
    }

    [Fact]
    public void Amending_cannot_resolve_a_defect_from_the_wire()
    {
        // The back-door guard: resolution fields on incoming records are discarded
        // unconditionally, so ResolveDefect and work-order completion stay the only two ways in.
        var inspection = TestInspections.PreTrip(defects: [Defect("Defroster")]);

        var result = TestInspections.AmendWith(inspection, [
            new InspectionDefect
            {
                Item = "Defroster",
                Severity = InspectionDefectSeverity.Major,
                Note = "as found",
                ResolutionReason = DefectResolutionReason.ReportedInError,
                ResolutionNote = "please go away",
                ResolvedBy = "Anyone At All",
                ResolvedAtUtc = ResolvedAt,
                ResolvedByWorkOrderId = Guid.NewGuid(),
            },
        ]);

        Assert.True(result.IsSuccess);

        var defroster = Assert.Single(inspection.Defects);
        Assert.False(defroster.IsResolved);
        Assert.Null(defroster.ResolutionReason);
        Assert.Null(defroster.ResolutionNote);
        Assert.Null(defroster.ResolvedBy);
        Assert.Null(defroster.ResolvedAtUtc);
        Assert.Null(defroster.ResolvedByWorkOrderId);
    }

    [Fact]
    public void Amending_carries_a_resolution_across_a_trimmed_and_differently_cased_item()
    {
        var (inspection, _) = WithOneResolvedDefect();

        var result = TestInspections.AmendWith(inspection, [Defect("  brakes "), Defect("Defroster")]);

        Assert.True(result.IsSuccess);

        var brakes = inspection.Defects.Single(d => d.Item == "  brakes ");
        Assert.True(brakes.IsResolved);
        Assert.Equal("M. Cardinal", brakes.ResolvedBy);
    }

    [Fact]
    public void Amending_that_renames_an_item_does_not_carry_the_resolution()
    {
        // The documented, accepted limitation: a renamed item is a different defect. Pinned by a
        // test so that "fixing" it later is a deliberate decision rather than an accident — and
        // note this failure mode is VISIBLE (the row reappears as open and can be re-resolved),
        // unlike the silent wipe the merge exists to prevent.
        var (inspection, _) = WithOneResolvedDefect();

        var result = TestInspections.AmendWith(inspection, [Defect("Brake lines"), Defect("Defroster")]);

        Assert.True(result.IsSuccess);

        var renamed = inspection.Defects.Single(d => d.Item == "Brake lines");
        Assert.False(renamed.IsResolved);
        Assert.Null(renamed.ResolutionReason);
        Assert.Null(renamed.ResolvedBy);
    }

    [Fact]
    public void Amending_keeps_a_recurrence_pointer_supplied_on_the_wire()
    {
        // RecurrenceOfInspectionId is NOT stripped: unlike the resolution stamp it is something
        // a dispatcher legitimately asserts when re-reporting a fault that was cleared before.
        var inspection = TestInspections.PreTrip(defects: [Defect("Defroster")]);
        var earlierInspectionId = Guid.NewGuid();

        var result = TestInspections.AmendWith(inspection, [
            Defect("Defroster") with { RecurrenceOfInspectionId = earlierInspectionId },
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(earlierInspectionId, Assert.Single(inspection.Defects).RecurrenceOfInspectionId);
    }
}

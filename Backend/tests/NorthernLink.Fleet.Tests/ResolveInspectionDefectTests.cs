using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The domain rules for clearing a defect: <see cref="VehicleInspection.ResolveDefect"/> stamps
/// one item, <see cref="VehicleInspection.ResolveDefectsForWorkOrder"/> stamps the whole
/// inspection on completion, and <c>Item</c> is the addressing key — so entry and amendment both
/// reject a duplicate. Resolution is final in both directions: there is no reopen, and a second
/// resolve is a conflict rather than a re-stamp.
/// </summary>
public class ResolveInspectionDefectTests
{
    private static readonly DateTimeOffset ResolvedAt =
        new(2026, 3, 14, 17, 5, 0, TimeSpan.Zero);

    private static InspectionDefect Defect(string item, InspectionDefectSeverity severity = InspectionDefectSeverity.Major) =>
        new() { Item = item, Severity = severity, Note = "as found" };

    [Fact]
    public void Resolving_a_defect_stamps_reason_note_who_and_when()
    {
        var inspection = TestInspections.PreTrip(defects: [Defect("Brakes"), Defect("Defroster")]);

        var result = inspection.ResolveDefect(
            "Brakes", DefectResolutionReason.PreviouslyRepaired, "Fixed at Thompson", "R. Ballantyne", ResolvedAt);

        Assert.True(result.IsSuccess);

        var brakes = inspection.Defects.Single(d => d.Item == "Brakes");
        Assert.True(brakes.IsResolved);
        Assert.Equal(DefectResolutionReason.PreviouslyRepaired, brakes.ResolutionReason);
        Assert.Equal("Fixed at Thompson", brakes.ResolutionNote);
        Assert.Equal("R. Ballantyne", brakes.ResolvedBy);
        Assert.Equal(ResolvedAt, brakes.ResolvedAtUtc);
        Assert.Null(brakes.ResolvedByWorkOrderId);

        // The other defect is untouched — resolution is per item, not per inspection.
        Assert.False(inspection.Defects.Single(d => d.Item == "Defroster").IsResolved);
    }

    [Fact]
    public void Resolving_matches_the_item_trimmed_and_case_insensitively()
    {
        var inspection = TestInspections.PreTrip(defects: [Defect("Brakes")]);

        var result = inspection.ResolveDefect(
            "  brakes ", DefectResolutionReason.ReportedInError, null, "Dispatch", ResolvedAt);

        Assert.True(result.IsSuccess);
        Assert.True(inspection.Defects.Single().IsResolved);
    }

    [Fact]
    public void Resolving_an_already_resolved_defect_fails_with_already_resolved()
    {
        var inspection = TestInspections.PreTrip(defects: [Defect("Brakes")]);
        Assert.True(inspection
            .ResolveDefect("Brakes", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", ResolvedAt)
            .IsSuccess);

        var second = inspection.ResolveDefect(
            "Brakes", DefectResolutionReason.ReportedInError, "oops", "Someone else", ResolvedAt.AddHours(1));

        Assert.True(second.IsFailure);
        Assert.Equal(InspectionErrors.DefectAlreadyResolved, second.Error);

        // The first stamp stands — a second resolve never overwrites the audit record.
        var brakes = inspection.Defects.Single();
        Assert.Equal(DefectResolutionReason.PreviouslyRepaired, brakes.ResolutionReason);
        Assert.Equal("Dispatch", brakes.ResolvedBy);
    }

    [Fact]
    public void Resolving_an_unknown_item_fails_with_defect_not_found()
    {
        var inspection = TestInspections.PreTrip(defects: [Defect("Brakes")]);

        var result = inspection.ResolveDefect(
            "Headlights", DefectResolutionReason.ReportedInError, null, "Dispatch", ResolvedAt);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.DefectNotFound, result.Error);
        Assert.False(inspection.Defects.Single().IsResolved);
    }

    [Fact]
    public void Resolving_raises_the_defects_resolved_event_so_the_read_model_reprojects()
    {
        var inspection = TestInspections.PreTrip(defects: [Defect("Brakes")]);
        inspection.ClearDomainEvents();

        Assert.True(inspection
            .ResolveDefect("Brakes", DefectResolutionReason.AcceptedMonitoring, null, "Dispatch", ResolvedAt)
            .IsSuccess);

        var raised = Assert.Single(inspection.DomainEvents.OfType<VehicleInspectionDefectsResolvedDomainEvent>());
        Assert.Equal(inspection.Id, raised.InspectionId);
        Assert.Equal(inspection.TenantId, raised.TenantId);
    }

    [Fact]
    public void Entering_two_defects_with_the_same_item_fails_with_duplicate_defect_item()
    {
        var result = VehicleInspection.Enter(
            TestVehicles.TenantId,
            InspectionSource.Dispatcher,
            InspectionType.PreTrip,
            tripNumber: null,
            vehicleId: null,
            unit: "U-04",
            driverName: "J. Spence",
            enteredBy: null,
            performedAt: DateTimeOffset.UtcNow,
            odometerKm: 118_204,
            checklistItems: [],
            defects: [Defect("Brakes"), Defect(" brakes ", InspectionDefectSeverity.Minor)],
            weather: [],
            temperatureC: null,
            roadConditions: [],
            visibility: null,
            roadAdvisories: null,
            fuelLevel: null,
            issues: [],
            attestations: [],
            driverSignatureName: null,
            certifiedAt: null,
            fuelAdded: false,
            fuelLitres: null,
            fuelCostCad: null);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.DuplicateDefectItem, result.Error);
    }

    [Fact]
    public void Amending_in_two_defects_with_the_same_item_fails_with_duplicate_defect_item()
    {
        var inspection = TestInspections.PreTrip(defects: [Defect("Brakes")]);

        var result = TestInspections.AmendWith(
            inspection, [Defect("Brakes"), Defect("BRAKES", InspectionDefectSeverity.Minor)]);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.DuplicateDefectItem, result.Error);
        Assert.Single(inspection.Defects);
    }

    [Fact]
    public void Entering_strips_resolution_fields_smuggled_in_on_an_incoming_defect()
    {
        // The create path must never be a back door around ResolveDefect: a caller that
        // hand-builds a resolved InspectionDefect gets it back open.
        var smuggled = new InspectionDefect
        {
            Item = "Brakes",
            Severity = InspectionDefectSeverity.OutOfService,
            Note = "weeping line",
            ResolutionReason = DefectResolutionReason.ReportedInError,
            ResolutionNote = "nothing to see here",
            ResolvedBy = "Nobody",
            ResolvedAtUtc = ResolvedAt,
            ResolvedByWorkOrderId = Guid.NewGuid(),
            RecurrenceOfInspectionId = Guid.NewGuid(),
        };

        var inspection = TestInspections.PreTrip(defects: [smuggled]);

        var stored = Assert.Single(inspection.Defects);
        Assert.False(stored.IsResolved);
        Assert.Null(stored.ResolutionReason);
        Assert.Null(stored.ResolutionNote);
        Assert.Null(stored.ResolvedBy);
        Assert.Null(stored.ResolvedAtUtc);
        Assert.Null(stored.ResolvedByWorkOrderId);

        // The report itself is kept verbatim, recurrence pointer included — that one is a
        // legitimate thing for a re-report to say.
        Assert.Equal(InspectionDefectSeverity.OutOfService, stored.Severity);
        Assert.Equal("weeping line", stored.Note);
        Assert.Equal(smuggled.RecurrenceOfInspectionId, stored.RecurrenceOfInspectionId);
    }

    [Fact]
    public void Work_order_completion_stamps_only_the_unresolved_defects()
    {
        var inspection = TestInspections.PreTrip(defects: [Defect("Brakes"), Defect("Defroster")]);
        Assert.True(inspection
            .ResolveDefect("Brakes", DefectResolutionReason.ReportedInError, "typo", "Dispatch", ResolvedAt)
            .IsSuccess);

        var workOrderId = Guid.NewGuid();
        inspection.ResolveDefectsForWorkOrder(workOrderId, "M. Cardinal", ResolvedAt.AddDays(1));

        // Already resolved: left exactly as it was, not re-attributed to the work order.
        var brakes = inspection.Defects.Single(d => d.Item == "Brakes");
        Assert.Equal(DefectResolutionReason.ReportedInError, brakes.ResolutionReason);
        Assert.Equal("Dispatch", brakes.ResolvedBy);
        Assert.Null(brakes.ResolvedByWorkOrderId);

        var defroster = inspection.Defects.Single(d => d.Item == "Defroster");
        Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, defroster.ResolutionReason);
        Assert.Equal("M. Cardinal", defroster.ResolvedBy);
        Assert.Equal(ResolvedAt.AddDays(1), defroster.ResolvedAtUtc);
        Assert.Equal(workOrderId, defroster.ResolvedByWorkOrderId);
    }

    [Fact]
    public void Work_order_completion_is_idempotent_and_a_no_op_run_writes_nothing()
    {
        var inspection = TestInspections.PreTrip(defects: [Defect("Brakes")]);
        var workOrderId = Guid.NewGuid();

        inspection.ResolveDefectsForWorkOrder(workOrderId, "M. Cardinal", ResolvedAt);
        var afterFirst = inspection.Defects.Single();

        inspection.ClearDomainEvents();
        inspection.ResolveDefectsForWorkOrder(workOrderId, "Someone else", ResolvedAt.AddDays(30));

        // Structural equality on the record: nothing about the stamp moved.
        Assert.Equal(afterFirst, inspection.Defects.Single());

        // And no event — an eventless write is rejected by the audit pipeline, so a no-op run
        // has to also be a no-write.
        Assert.Empty(inspection.DomainEvents.OfType<VehicleInspectionDefectsResolvedDomainEvent>());
    }
}

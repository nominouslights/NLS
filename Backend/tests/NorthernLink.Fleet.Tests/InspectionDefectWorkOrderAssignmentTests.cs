using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The inspection side of the per-defect link: <see cref="InspectionDefect.WorkOrderId"/> is set
/// only while the defect is on an active work order, every real change raises an event (the
/// audit pipeline rejects an eventless write), and a no-op raises nothing.
/// </summary>
public class InspectionDefectWorkOrderAssignmentTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static InspectionDefect Defect(string item) =>
        new() { Item = item, Severity = InspectionDefectSeverity.Major, Note = "as found" };

    private static VehicleInspection Fresh(params string[] items)
    {
        var inspection = TestInspections.PreTrip(defects: [.. items.Select(Defect)]);
        inspection.ClearDomainEvents();
        return inspection;
    }

    private static int LinkEvents(VehicleInspection inspection) =>
        inspection.DomainEvents.OfType<VehicleInspectionDefectWorkOrderChangedDomainEvent>().Count();

    [Fact]
    public void Assigning_attaches_the_defect_and_raises_an_event()
    {
        var inspection = Fresh("Brakes", "Wipers");
        var workOrderId = Guid.NewGuid();

        var result = inspection.AssignDefectToWorkOrder(" brakes ", workOrderId);

        Assert.True(result.IsSuccess);
        Assert.Equal(workOrderId, inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.Null(inspection.FindDefect("Wipers")!.WorkOrderId);
        Assert.False(inspection.FindDefect("Brakes")!.IsResolved);
        Assert.True(inspection.HasDefectOnActiveWorkOrder);

        var raised = Assert.Single(inspection.DomainEvents.OfType<VehicleInspectionDefectWorkOrderChangedDomainEvent>());
        Assert.Equal("Brakes", raised.Item);
        Assert.Equal(workOrderId, raised.WorkOrderId);
    }

    [Fact]
    public void A_defect_already_on_a_work_order_cannot_be_assigned_again()
    {
        var inspection = Fresh("Brakes");
        var first = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", first).IsSuccess);

        var again = inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid());
        var same = inspection.AssignDefectToWorkOrder("Brakes", first);

        Assert.Equal(InspectionErrors.DefectAlreadyOnWorkOrder, again.Error);
        Assert.Equal(InspectionErrors.DefectAlreadyOnWorkOrder, same.Error);
        Assert.Equal(first, inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.Equal(1, LinkEvents(inspection));
    }

    [Fact]
    public void A_resolved_or_unknown_defect_cannot_be_assigned()
    {
        var inspection = Fresh("Brakes");
        Assert.True(inspection.ResolveDefect("Brakes", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", At).IsSuccess);

        Assert.Equal(InspectionErrors.DefectAlreadyResolved, inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid()).Error);
        Assert.Equal(InspectionErrors.DefectNotFound, inspection.AssignDefectToWorkOrder("Mirrors", Guid.NewGuid()).Error);
        Assert.Equal(0, LinkEvents(inspection));
    }

    [Fact]
    public void Releasing_detaches_the_defect_and_leaves_it_open()
    {
        var inspection = Fresh("Brakes");
        var workOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId).IsSuccess);

        inspection.ReleaseDefectFromWorkOrder("BRAKES", workOrderId);

        var brakes = inspection.FindDefect("Brakes")!;
        Assert.Null(brakes.WorkOrderId);
        Assert.False(brakes.IsResolved);
        Assert.False(inspection.HasDefectOnActiveWorkOrder);
        Assert.Equal(2, LinkEvents(inspection));
        Assert.Null(inspection.DomainEvents.OfType<VehicleInspectionDefectWorkOrderChangedDomainEvent>().Last().WorkOrderId);

        // Free again — a later work order can take it.
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid()).IsSuccess);
    }

    [Fact]
    public void Releasing_from_a_different_work_order_or_a_missing_item_is_a_silent_no_op()
    {
        var inspection = Fresh("Brakes", "Wipers");
        var workOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId).IsSuccess);
        inspection.ClearDomainEvents();

        inspection.ReleaseDefectFromWorkOrder("Brakes", Guid.NewGuid());
        inspection.ReleaseDefectFromWorkOrder("Wipers", workOrderId);
        inspection.ReleaseDefectFromWorkOrder("Mirrors", workOrderId);

        Assert.Equal(workOrderId, inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.Empty(inspection.DomainEvents);
    }

    [Fact]
    public void Resolving_under_the_work_order_stamps_it_and_moves_the_link_to_the_resolution()
    {
        var inspection = Fresh("Brakes");
        var workOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId).IsSuccess);

        inspection.ResolveDefectUnderWorkOrder(
            "Brakes", workOrderId, DefectResolutionReason.NoFaultFound, " checked, fine ", "M. Cardinal", At);

        var brakes = inspection.FindDefect("Brakes")!;
        Assert.Null(brakes.WorkOrderId);
        Assert.Equal(workOrderId, brakes.ResolvedByWorkOrderId);
        Assert.Equal(DefectResolutionReason.NoFaultFound, brakes.ResolutionReason);
        Assert.Equal("checked, fine", brakes.ResolutionNote);
        Assert.Equal("M. Cardinal", brakes.ResolvedBy);
        Assert.Equal(At, brakes.ResolvedAtUtc);
        Assert.Single(inspection.DomainEvents.OfType<VehicleInspectionDefectsResolvedDomainEvent>());
    }

    [Fact]
    public void Resolving_a_defect_already_resolved_by_hand_keeps_its_stamp_and_only_detaches_it()
    {
        var inspection = Fresh("Brakes");
        var workOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId).IsSuccess);
        Assert.True(inspection.ResolveDefect("Brakes", DefectResolutionReason.ReportedInError, "not faulty", "Dispatch", At).IsSuccess);

        // Hand-resolving does not detach: the work order is still open and still lists it.
        Assert.Equal(workOrderId, inspection.FindDefect("Brakes")!.WorkOrderId);
        inspection.ClearDomainEvents();

        inspection.ResolveDefectUnderWorkOrder(
            "Brakes", workOrderId, DefectResolutionReason.RepairedUnderWorkOrder, null, "M. Cardinal", At.AddHours(1));

        var brakes = inspection.FindDefect("Brakes")!;
        Assert.Equal(DefectResolutionReason.ReportedInError, brakes.ResolutionReason);
        Assert.Equal("Dispatch", brakes.ResolvedBy);
        Assert.Null(brakes.ResolvedByWorkOrderId);
        Assert.Null(brakes.WorkOrderId);
        Assert.Equal(1, LinkEvents(inspection));
    }

    [Fact]
    public void Resolving_a_defect_that_is_not_on_that_work_order_is_a_silent_no_op()
    {
        var inspection = Fresh("Brakes", "Wipers");
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid()).IsSuccess);
        inspection.ClearDomainEvents();

        var workOrderId = Guid.NewGuid();
        inspection.ResolveDefectUnderWorkOrder("Brakes", workOrderId, DefectResolutionReason.RepairedUnderWorkOrder, null, "M", At);
        inspection.ResolveDefectUnderWorkOrder("Wipers", workOrderId, DefectResolutionReason.RepairedUnderWorkOrder, null, "M", At);
        inspection.ResolveDefectUnderWorkOrder("Mirrors", workOrderId, DefectResolutionReason.RepairedUnderWorkOrder, null, "M", At);

        Assert.All(inspection.Defects, d => Assert.False(d.IsResolved));
        Assert.Empty(inspection.DomainEvents);
    }

    [Fact]
    public void Amending_keeps_a_surviving_defect_on_its_work_order_and_drops_a_removed_one()
    {
        var inspection = Fresh("Brakes", "Wipers");
        var workOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId).IsSuccess);

        // Off the wire: no WorkOrderId on the incoming records, severity corrected.
        var amended = TestInspections.AmendWith(inspection, [
            new InspectionDefect { Item = "brakes", Severity = InspectionDefectSeverity.OutOfService, Note = "worse" },
            new InspectionDefect { Item = "Horn", Severity = InspectionDefectSeverity.Minor, WorkOrderId = Guid.NewGuid() },
        ]);

        Assert.True(amended.IsSuccess);
        var brakes = inspection.FindDefect("Brakes")!;
        Assert.Equal(workOrderId, brakes.WorkOrderId);
        Assert.Equal(InspectionDefectSeverity.OutOfService, brakes.Severity);
        // A new defect starts unattached, whatever the wire claimed.
        Assert.Null(inspection.FindDefect("Horn")!.WorkOrderId);
    }

    [Fact]
    public void Entering_an_inspection_strips_any_work_order_link_from_the_wire()
    {
        var inspection = TestInspections.PreTrip(defects: [
            new InspectionDefect { Item = "Brakes", Severity = InspectionDefectSeverity.Major, WorkOrderId = Guid.NewGuid() },
        ]);

        Assert.Null(Assert.Single(inspection.Defects).WorkOrderId);
        Assert.False(inspection.HasDefectOnActiveWorkOrder);
    }

    [Fact]
    public void A_legacy_work_order_completion_never_sweeps_a_defect_attached_to_another_work_order()
    {
        var inspection = Fresh("Brakes", "Wipers");
        var activeWorkOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", activeWorkOrderId).IsSuccess);

        // items: null = the legacy "resolve every open defect" fallback.
        inspection.ResolveDefectsForWorkOrder(Guid.NewGuid(), "M. Cardinal", At, items: null);

        Assert.False(inspection.FindDefect("Brakes")!.IsResolved);
        Assert.Equal(activeWorkOrderId, inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.True(inspection.FindDefect("Wipers")!.IsResolved);
    }
}

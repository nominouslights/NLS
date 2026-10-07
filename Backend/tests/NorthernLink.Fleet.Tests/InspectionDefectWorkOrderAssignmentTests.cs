using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using NorthernLink.Shared.Kernel;
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

        var result = inspection.AssignDefectToWorkOrder(" brakes ", workOrderId, generatedWorkOrderIsOpen: false);

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
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", first, generatedWorkOrderIsOpen: false).IsSuccess);

        var again = inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid(), generatedWorkOrderIsOpen: false);
        var same = inspection.AssignDefectToWorkOrder("Brakes", first, generatedWorkOrderIsOpen: false);

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

        Assert.Equal(InspectionErrors.DefectAlreadyResolved, inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid(), generatedWorkOrderIsOpen: false).Error);
        Assert.Equal(InspectionErrors.DefectNotFound, inspection.AssignDefectToWorkOrder("Mirrors", Guid.NewGuid(), generatedWorkOrderIsOpen: false).Error);
        Assert.Equal(0, LinkEvents(inspection));
    }

    [Fact]
    public void Releasing_detaches_the_defect_and_leaves_it_open()
    {
        var inspection = Fresh("Brakes");
        var workOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId, generatedWorkOrderIsOpen: false).IsSuccess);

        inspection.ReleaseDefectFromWorkOrder("BRAKES", workOrderId);

        var brakes = inspection.FindDefect("Brakes")!;
        Assert.Null(brakes.WorkOrderId);
        Assert.False(brakes.IsResolved);
        Assert.False(inspection.HasDefectOnActiveWorkOrder);
        Assert.Equal(2, LinkEvents(inspection));
        Assert.Null(inspection.DomainEvents.OfType<VehicleInspectionDefectWorkOrderChangedDomainEvent>().Last().WorkOrderId);

        // Free again — a later work order can take it.
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid(), generatedWorkOrderIsOpen: false).IsSuccess);
    }

    [Fact]
    public void Releasing_from_a_different_work_order_or_a_missing_item_is_a_silent_no_op()
    {
        var inspection = Fresh("Brakes", "Wipers");
        var workOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId, generatedWorkOrderIsOpen: false).IsSuccess);
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
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId, generatedWorkOrderIsOpen: false).IsSuccess);

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
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId, generatedWorkOrderIsOpen: false).IsSuccess);
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
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid(), generatedWorkOrderIsOpen: false).IsSuccess);
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
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId, generatedWorkOrderIsOpen: false).IsSuccess);

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

    [Theory]
    [InlineData(null)] // the attached item simply left out
    [InlineData("Brake lines")] // renamed — a renamed item is a different defect, so this drops "Brakes"
    public void Amending_out_a_defect_that_is_on_an_open_work_order_is_refused_and_changes_nothing(string? replacement)
    {
        var inspection = Fresh("Brakes", "Wipers");
        var workOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId, generatedWorkOrderIsOpen: false).IsSuccess);
        inspection.ClearDomainEvents();
        var before = inspection.Defects;
        var odometerBefore = inspection.OdometerKm;

        var amended = TestInspections.AmendWith(
            inspection, replacement is null ? [Defect("Wipers")] : [Defect("Wipers"), Defect(replacement)], odometerKm: 999_999);

        Assert.True(amended.IsFailure);
        Assert.Equal(InspectionErrors.DefectOnActiveWorkOrder.Code, amended.Error.Code);
        Assert.Equal(ErrorType.Conflict, amended.Error.Type);
        Assert.Equal(InspectionErrors.AttachedDefectCannotBeDropped("Brakes"), amended.Error);
        Assert.Contains("\"Brakes\"", amended.Error.Message);

        Assert.Same(before, inspection.Defects);
        Assert.Equal(workOrderId, inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.Equal(odometerBefore, inspection.OdometerKm);
        Assert.Empty(inspection.DomainEvents);
    }

    [Fact]
    public void Amending_out_a_defect_that_is_not_on_a_work_order_is_fine()
    {
        var inspection = Fresh("Brakes", "Wipers");
        var workOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId, generatedWorkOrderIsOpen: false).IsSuccess);

        var amended = TestInspections.AmendWith(inspection, [Defect("BRAKES")]);

        Assert.True(amended.IsSuccess);
        Assert.Null(inspection.FindDefect("Wipers"));
        Assert.Equal(workOrderId, Assert.Single(inspection.Defects).WorkOrderId);
    }

    [Fact]
    public void Amending_the_severity_and_note_of_a_defect_on_an_open_work_order_is_fine()
    {
        var inspection = Fresh("Brakes");
        var workOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId, generatedWorkOrderIsOpen: false).IsSuccess);

        var amended = TestInspections.AmendWith(inspection, [
            new InspectionDefect { Item = " Brakes ", Severity = InspectionDefectSeverity.Minor, Note = "pads only" },
        ]);

        Assert.True(amended.IsSuccess);
        var brakes = inspection.FindDefect("Brakes")!;
        Assert.Equal(workOrderId, brakes.WorkOrderId);
        Assert.Equal(InspectionDefectSeverity.Minor, brakes.Severity);
        Assert.Equal("pads only", brakes.Note);
    }

    [Fact]
    public void Moving_an_inspection_to_another_vehicle_while_a_defect_is_on_an_open_work_order_is_refused()
    {
        var vehicleId = Guid.NewGuid();
        var linked = TestInspections.PreTrip(vehicleId: vehicleId, defects: [Defect("Brakes"), Defect("Wipers")]);
        var unitOnly = TestInspections.PreTrip(vehicleId: null, unit: "U-04", defects: [Defect("Brakes"), Defect("Wipers")]);
        foreach (var inspection in new[] { linked, unitOnly })
        {
            // Only "Brakes" is attached — ANY attached defect locks the vehicle.
            Assert.True(inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid(), generatedWorkOrderIsOpen: false).IsSuccess);
            inspection.ClearDomainEvents();
        }

        Result[] attempts =
        [
            // Linked → another vehicle.
            TestInspections.AmendWith(linked, [Defect("Brakes"), Defect("Wipers")], replaceVehicleId: true, vehicleId: Guid.NewGuid()),
            // Linked → unlinked.
            TestInspections.AmendWith(linked, [Defect("Brakes"), Defect("Wipers")], replaceVehicleId: true, vehicleId: null),
            // Unit-only → another unit.
            TestInspections.AmendWith(unitOnly, [Defect("Brakes"), Defect("Wipers")], unit: "U-07"),
            // Unit-only → linked: this aggregate cannot tell whether the link names the same truck.
            TestInspections.AmendWith(unitOnly, [Defect("Brakes"), Defect("Wipers")], replaceVehicleId: true, vehicleId: Guid.NewGuid()),
        ];

        Assert.All(attempts, a =>
        {
            Assert.True(a.IsFailure);
            Assert.Equal(InspectionErrors.VehicleChangeWithDefectOnActiveWorkOrder, a.Error);
            Assert.Equal(ErrorType.Conflict, a.Error.Type);
        });
        Assert.Equal(vehicleId, linked.VehicleId);
        Assert.Null(unitOnly.VehicleId);
        Assert.Equal("U-04", unitOnly.Unit);
        Assert.Empty(linked.DomainEvents);
        Assert.Empty(unitOnly.DomainEvents);
    }

    [Fact]
    public void Moving_an_inspection_to_another_vehicle_with_no_defect_on_an_open_work_order_is_fine()
    {
        var inspection = TestInspections.PreTrip(vehicleId: Guid.NewGuid(), defects: [Defect("Brakes")]);
        var workOrderId = Guid.NewGuid();
        // Once on a work order, but released — free again, so the vehicle is no longer locked.
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", workOrderId, generatedWorkOrderIsOpen: false).IsSuccess);
        inspection.ReleaseDefectFromWorkOrder("Brakes", workOrderId);
        var otherVehicleId = Guid.NewGuid();

        var amended = TestInspections.AmendWith(
            inspection, [Defect("Brakes")], unit: "U-07", replaceVehicleId: true, vehicleId: otherVehicleId);

        Assert.True(amended.IsSuccess);
        Assert.Equal(otherVehicleId, inspection.VehicleId);
        Assert.Equal("U-07", inspection.Unit);
    }

    [Fact]
    public void Correcting_the_unit_text_of_a_linked_inspection_with_an_attached_defect_is_fine()
    {
        // With a hard vehicle link the unit is display text; the truck has not changed.
        var vehicleId = Guid.NewGuid();
        var inspection = TestInspections.PreTrip(vehicleId: vehicleId, unit: "U-4", defects: [Defect("Brakes")]);
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid(), generatedWorkOrderIsOpen: false).IsSuccess);

        var amended = TestInspections.AmendWith(inspection, [Defect("Brakes")], unit: "U-04");

        Assert.True(amended.IsSuccess);
        Assert.Equal(vehicleId, inspection.VehicleId);
        Assert.Equal("U-04", inspection.Unit);
    }

    [Fact]
    public void An_open_legacy_generated_work_order_holds_every_unresolved_unattached_defect()
    {
        var inspection = Fresh("Brakes", "Wipers", "Horn");
        Assert.True(inspection.ResolveDefect("Horn", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", At).IsSuccess);
        Assert.True(inspection.LinkWorkOrder(Guid.NewGuid()).IsSuccess);
        inspection.ClearDomainEvents();

        Assert.Equal(InspectionErrors.DefectAlreadyOnWorkOrder, inspection.CanAttachDefect("Brakes", generatedWorkOrderIsOpen: true).Error);
        Assert.Equal(InspectionErrors.DefectAlreadyResolved, inspection.CanAttachDefect("Horn", generatedWorkOrderIsOpen: true).Error);
        Assert.Empty(inspection.AttachableDefects(generatedWorkOrderIsOpen: true));
        Assert.Equal(
            InspectionErrors.DefectAlreadyOnWorkOrder,
            inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid(), generatedWorkOrderIsOpen: true).Error);
        Assert.Empty(inspection.DomainEvents);

        // Once that work order is closed, the same defects are free.
        Assert.True(inspection.CanAttachDefect("Brakes", generatedWorkOrderIsOpen: false).IsSuccess);
        Assert.Equal(["Brakes", "Wipers"], inspection.AttachableDefects(generatedWorkOrderIsOpen: false).Select(d => d.Item));
    }

    [Fact]
    public void Amend_applies_the_same_legacy_hold_as_attaching()
    {
        // One inspection, one open legacy generated work order: whatever CanAttachDefect calls
        // held, Amend may neither drop nor move; whatever it calls free (resolved), Amend may drop.
        var inspection = Fresh("Brakes", "Wipers");
        Assert.True(inspection.ResolveDefect("Wipers", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", At).IsSuccess);
        Assert.True(inspection.LinkWorkOrder(Guid.NewGuid()).IsSuccess);
        Assert.Equal(InspectionErrors.DefectAlreadyOnWorkOrder, inspection.CanAttachDefect("Brakes", generatedWorkOrderIsOpen: true).Error);

        var drop = TestInspections.AmendWith(inspection, [Defect("Wipers")], generatedWorkOrderIsOpen: true);
        var move = TestInspections.AmendWith(inspection, [Defect("Brakes"), Defect("Wipers")], unit: "U-07", generatedWorkOrderIsOpen: true);

        Assert.Equal(InspectionErrors.AttachedDefectCannotBeDropped("Brakes"), drop.Error);
        Assert.Equal(InspectionErrors.VehicleChangeWithDefectOnActiveWorkOrder, move.Error);

        Assert.True(TestInspections.AmendWith(inspection, [Defect("Brakes")], generatedWorkOrderIsOpen: true).IsSuccess);
        Assert.Null(inspection.FindDefect("Wipers"));

        // Closed legacy work order: nothing held, both amends go through.
        Assert.True(TestInspections.AmendWith(inspection, [Defect("Brakes")], unit: "U-07", generatedWorkOrderIsOpen: false).IsSuccess);
        Assert.True(TestInspections.AmendWith(inspection, [], unit: "U-07", generatedWorkOrderIsOpen: false).IsSuccess);
        Assert.Empty(inspection.Defects);
    }

    [Fact]
    public void With_no_generated_work_order_the_open_flag_holds_nothing()
    {
        var inspection = Fresh("Brakes");

        Assert.True(inspection.CanAttachDefect("Brakes", generatedWorkOrderIsOpen: true).IsSuccess);
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", Guid.NewGuid(), generatedWorkOrderIsOpen: true).IsSuccess);
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
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", activeWorkOrderId, generatedWorkOrderIsOpen: false).IsSuccess);

        // items: null = the legacy "resolve every open defect" fallback.
        inspection.ResolveDefectsForWorkOrder(Guid.NewGuid(), "M. Cardinal", At, items: null);

        Assert.False(inspection.FindDefect("Brakes")!.IsResolved);
        Assert.Equal(activeWorkOrderId, inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.True(inspection.FindDefect("Wipers")!.IsResolved);
    }
}

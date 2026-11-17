using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The duplicate-defect rule: a known fault re-reported on every DVIR until it is fixed leaves
/// one open defect per inspection for one component, and resolving any one must clear them all —
/// same vehicle, same component (trimmed, case-insensitive), still open, on another inspection.
/// </summary>
public class DuplicateDefectsTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 11, 2, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResolvedAt = new(2026, 11, 5, 15, 30, 0, TimeSpan.Zero);
    private const string Unit = "U-04";

    private static InspectionDefect Defect(string item, InspectionDefectSeverity severity = InspectionDefectSeverity.Major) =>
        new() { Item = item, Severity = severity, Note = "as found" };

    private static VehicleInspection Inspection(
        Guid? vehicleId,
        int day,
        string unit = Unit,
        params InspectionDefect[] defects) =>
        TestInspections.PreTrip(
            vehicleId: vehicleId,
            unit: unit,
            defects: defects,
            performedAt: Day1.AddDays(day),
            tripNumber: $"TR-{4800 + day}");

    private static void ResolveByHand(VehicleInspection inspection, string item, string? note = "replaced at Thompson") =>
        Assert.True(inspection
            .ResolveDefect(item, DefectResolutionReason.PreviouslyRepaired, note, "R. Ballantyne", ResolvedAt)
            .IsSuccess);

    private static IReadOnlyList<DefectOnInspection> Cascade(
        VehicleInspection source,
        string item,
        string? vehicleUnitNumber,
        params VehicleInspection[] candidates) =>
        DuplicateDefects.Resolve(source, item, DefectVehicleScope.Of(source, vehicleUnitNumber), candidates);

    [Fact]
    public void A_manual_resolve_clears_the_same_component_on_every_other_inspection_of_the_vehicle()
    {
        var vehicleId = Guid.NewGuid();
        var day1 = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        var day2 = Inspection(vehicleId, 2, defects: [Defect("Mirror"), Defect("Wipers")]);
        var day3 = Inspection(vehicleId, 3, defects: Defect("Mirror", InspectionDefectSeverity.Minor));

        // The dispatcher clears the NEWEST report; the older two are its duplicates.
        ResolveByHand(day3, "Mirror");
        var cleared = Cascade(day3, "Mirror", Unit, day3, day2, day1);

        // Oldest report first, and never the addressed inspection itself.
        Assert.Equal([day1.Id, day2.Id], cleared.Select(c => c.Inspection.Id));

        var addressed = day3.FindDefect("Mirror")!;
        foreach (var inspection in new[] { day1, day2 })
        {
            var mirror = inspection.FindDefect("Mirror")!;
            Assert.Equal(DefectResolutionReason.PreviouslyRepaired, mirror.ResolutionReason);
            Assert.Equal("replaced at Thompson", mirror.ResolutionNote);
            Assert.Equal("R. Ballantyne", mirror.ResolvedBy);
            Assert.Equal(ResolvedAt, mirror.ResolvedAtUtc);
            Assert.Null(mirror.ResolvedByWorkOrderId);
            Assert.Equal(day3.Id, mirror.ResolvedWithInspectionId);
            // The report itself is untouched: severity and note are what the driver graded.
            Assert.Equal(InspectionDefectSeverity.Major, mirror.Severity);
            Assert.Equal("as found", mirror.Note);
        }

        // The addressed defect was resolved directly, so it carries no provenance.
        Assert.Null(addressed.ResolvedWithInspectionId);
        // Another component on a duplicate's inspection stays open.
        Assert.False(day2.FindDefect("Wipers")!.IsResolved);
    }

    [Fact]
    public void Each_touched_inspection_raises_its_own_resolved_event_and_an_untouched_one_raises_none()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 3, defects: Defect("Mirror"));
        var duplicate = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        var unrelated = Inspection(vehicleId, 2, defects: Defect("Horn"));
        duplicate.ClearDomainEvents();
        unrelated.ClearDomainEvents();

        ResolveByHand(addressed, "Mirror");
        Cascade(addressed, "Mirror", Unit, duplicate, unrelated);

        var raised = Assert.Single(duplicate.DomainEvents.OfType<VehicleInspectionDefectsResolvedDomainEvent>());
        Assert.Equal(duplicate.Id, raised.InspectionId);
        // An inspection with no change must not be modified: the audit pipeline rejects an
        // eventless write, so a skipped candidate raising nothing is what keeps it a no-write.
        Assert.Empty(unrelated.DomainEvents);
    }

    [Fact]
    public void An_already_resolved_duplicate_keeps_its_own_stamp()
    {
        var vehicleId = Guid.NewGuid();
        var earlier = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        Assert.True(earlier
            .ResolveDefect("Mirror", DefectResolutionReason.ReportedInError, "glare, not a crack", "Dispatch", Day1)
            .IsSuccess);
        earlier.ClearDomainEvents();

        ResolveByHand(addressed, "Mirror");
        var cleared = Cascade(addressed, "Mirror", Unit, earlier);

        Assert.Empty(cleared);
        var mirror = earlier.FindDefect("Mirror")!;
        Assert.Equal(DefectResolutionReason.ReportedInError, mirror.ResolutionReason);
        Assert.Equal("glare, not a crack", mirror.ResolutionNote);
        Assert.Equal(Day1, mirror.ResolvedAtUtc);
        Assert.Null(mirror.ResolvedWithInspectionId);
        Assert.Empty(earlier.DomainEvents);
    }

    [Fact]
    public void Another_vehicles_defect_for_the_same_component_is_untouched()
    {
        var addressed = Inspection(Guid.NewGuid(), 2, defects: Defect("Mirror"));
        var otherTruck = Inspection(Guid.NewGuid(), 1, unit: "U-07", defects: Defect("Mirror"));
        var otherTruckSameUnitText = Inspection(Guid.NewGuid(), 1, unit: Unit, defects: Defect("Mirror"));

        ResolveByHand(addressed, "Mirror");
        var cleared = Cascade(addressed, "Mirror", Unit, otherTruck, otherTruckSameUnitText);

        // A LINKED inspection of another vehicle never matches on unit text: the link wins.
        Assert.Empty(cleared);
        Assert.False(otherTruck.FindDefect("Mirror")!.IsResolved);
        Assert.False(otherTruckSameUnitText.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void Another_tenants_inspection_is_never_touched_even_if_the_query_returned_it()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var foreign = VehicleInspection.Enter(
            Guid.NewGuid(), InspectionSource.Dispatcher, InspectionType.PreTrip, "TR-9", vehicleId, Unit,
            "J. Spence", null, Day1, 1, [], [Defect("Mirror")], [], null, [], null, null, null, [], [], null,
            null, false, null, null).Value;

        ResolveByHand(addressed, "Mirror");

        Assert.Empty(Cascade(addressed, "Mirror", Unit, foreign));
        Assert.False(foreign.FindDefect("Mirror")!.IsResolved);
    }

    [Theory]
    [InlineData("mirror")]
    [InlineData("  MIRROR ")]
    [InlineData("Mirror\t")]
    public void Case_and_whitespace_variants_of_the_item_are_the_same_component(string variant)
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var duplicate = Inspection(vehicleId, 1, defects: Defect(variant));

        ResolveByHand(addressed, "Mirror");
        var cleared = Assert.Single(Cascade(addressed, "Mirror", Unit, duplicate));

        Assert.Equal(duplicate.Id, cleared.Inspection.Id);
        // The duplicate keeps its own spelling — only the resolution stamp is copied.
        Assert.Equal(variant, cleared.Defect.Item);
        Assert.True(duplicate.FindDefect(variant)!.IsResolved);
    }

    [Fact]
    public void A_similar_but_different_component_is_not_a_duplicate()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var other = Inspection(vehicleId, 1, defects: Defect("Mirror bracket"));

        ResolveByHand(addressed, "Mirror");

        Assert.Empty(Cascade(addressed, "Mirror", Unit, other));
        Assert.False(other.FindDefect("Mirror bracket")!.IsResolved);
    }

    [Fact]
    public void Legacy_unit_only_inspections_of_the_linked_vehicle_match_on_its_current_unit_number()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 3, unit: "U-04 (old)", defects: Defect("Mirror"));
        var legacy = Inspection(vehicleId: null, 1, unit: Unit, defects: Defect("Mirror"));
        var legacyOtherUnit = Inspection(vehicleId: null, 2, unit: "U-07", defects: Defect("Mirror"));

        ResolveByHand(addressed, "Mirror");
        // The vehicle's CURRENT unit number is U-04 — the panel's rule, not the inspection's text.
        var cleared = Cascade(addressed, "Mirror", Unit, legacy, legacyOtherUnit);

        Assert.Equal(legacy.Id, Assert.Single(cleared).Inspection.Id);
        Assert.False(legacyOtherUnit.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void With_the_linked_vehicle_unknown_only_the_id_matches()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 3, defects: Defect("Mirror"));
        var sameVehicle = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        var legacy = Inspection(vehicleId: null, 2, unit: Unit, defects: Defect("Mirror"));

        ResolveByHand(addressed, "Mirror");
        var cleared = Cascade(addressed, "Mirror", vehicleUnitNumber: null, sameVehicle, legacy);

        Assert.Equal(sameVehicle.Id, Assert.Single(cleared).Inspection.Id);
        Assert.False(legacy.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void A_unit_only_addressed_inspection_matches_on_its_unit_alone()
    {
        var addressed = Inspection(vehicleId: null, 3, defects: Defect("Mirror"));
        var legacy = Inspection(vehicleId: null, 1, defects: Defect("Mirror"));
        var linkedSameUnit = Inspection(Guid.NewGuid(), 2, defects: Defect("Mirror"));
        var otherUnit = Inspection(vehicleId: null, 2, unit: "U-07", defects: Defect("Mirror"));

        ResolveByHand(addressed, "Mirror");
        var cleared = Cascade(addressed, "Mirror", vehicleUnitNumber: "ignored", legacy, linkedSameUnit, otherUnit);

        Assert.Equal([legacy.Id, linkedSameUnit.Id], cleared.Select(c => c.Inspection.Id));
        Assert.False(otherUnit.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void A_duplicate_on_another_active_work_order_is_resolved_but_stays_on_that_work_order()
    {
        var vehicleId = Guid.NewGuid();
        var otherWorkOrder = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var attached = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        Assert.True(attached.AssignDefectToWorkOrder("Mirror", otherWorkOrder, generatedWorkOrderIsOpen: false).IsSuccess);

        ResolveByHand(addressed, "Mirror");
        Assert.Single(Cascade(addressed, "Mirror", Unit, attached));

        var mirror = attached.FindDefect("Mirror")!;
        Assert.True(mirror.IsResolved);
        // Exactly what a hand-resolve does: someone else's work order still lists it until it
        // completes (which then just detaches it) or lets go.
        Assert.Equal(otherWorkOrder, mirror.WorkOrderId);
        Assert.Null(mirror.ResolvedByWorkOrderId);

        // ...and that work order's completion only detaches it, keeping the duplicate's stamp.
        Assert.False(attached.ResolveDefectUnderWorkOrder(
            "Mirror", otherWorkOrder, DefectResolutionReason.RepairedUnderWorkOrder, null, "M", ResolvedAt.AddDays(1)));
        Assert.Null(attached.FindDefect("Mirror")!.WorkOrderId);
        Assert.Equal(addressed.Id, attached.FindDefect("Mirror")!.ResolvedWithInspectionId);
    }

    [Fact]
    public void A_duplicate_on_the_resolving_work_order_itself_is_detached()
    {
        var vehicleId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var duplicate = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        Assert.True(addressed.AssignDefectToWorkOrder("Mirror", workOrderId, false).IsSuccess);
        Assert.True(duplicate.AssignDefectToWorkOrder("Mirror", workOrderId, false).IsSuccess);

        Assert.True(addressed.ResolveDefectUnderWorkOrder(
            "Mirror", workOrderId, DefectResolutionReason.RepairedUnderWorkOrder, "new glass", "M. Cardinal", ResolvedAt));
        Assert.Single(Cascade(addressed, "Mirror", Unit, duplicate));

        var mirror = duplicate.FindDefect("Mirror")!;
        Assert.Equal(workOrderId, mirror.ResolvedByWorkOrderId);
        // That work order is closing: "WorkOrderId set ⇔ on an open work order" must hold.
        Assert.Null(mirror.WorkOrderId);
    }

    [Fact]
    public void A_duplicate_held_by_an_open_legacy_generated_work_order_is_resolved_and_no_longer_held()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var legacyHeld = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        Assert.True(legacyHeld.LinkWorkOrder(Guid.NewGuid()).IsSuccess);

        ResolveByHand(addressed, "Mirror");
        Assert.Single(Cascade(addressed, "Mirror", Unit, legacyHeld));

        Assert.True(legacyHeld.FindDefect("Mirror")!.IsResolved);
        // A resolved defect is not held by the legacy work order, whose completion skips it.
        Assert.Equal(
            InspectionErrors.DefectAlreadyResolved,
            legacyHeld.CanAttachDefect("Mirror", generatedWorkOrderIsOpen: true).Error);
    }

    [Fact]
    public void A_defect_reported_after_the_resolve_is_a_new_defect_not_a_duplicate()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        ResolveByHand(addressed, "Mirror");
        Cascade(addressed, "Mirror", Unit);

        // Reported the next morning — the cascade has already run, so it simply stays open.
        var recurrence = Inspection(vehicleId, 2, defects: Defect("Mirror"));

        Assert.False(recurrence.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void Resolving_duplicates_of_an_unresolved_defect_is_refused()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var duplicate = Inspection(vehicleId, 1, defects: Defect("Mirror"));

        Assert.Throws<InvalidOperationException>(() => Cascade(addressed, "Mirror", Unit, duplicate));
        Assert.Throws<ArgumentException>(() => duplicate.ResolveAsDuplicateOf(addressed.Id, addressed.FindDefect("Mirror")!));
        Assert.False(duplicate.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void An_inspection_is_never_a_duplicate_of_itself()
    {
        var addressed = Inspection(Guid.NewGuid(), 1, defects: Defect("Mirror"));
        ResolveByHand(addressed, "Mirror");
        addressed.ClearDomainEvents();

        Assert.Null(addressed.ResolveAsDuplicateOf(addressed.Id, addressed.FindDefect("Mirror")!));
        Assert.Empty(addressed.DomainEvents);
    }

    [Fact]
    public void Provenance_cannot_be_set_from_the_wire_and_survives_an_amend()
    {
        var vehicleId = Guid.NewGuid();
        var forged = Defect("Mirror") with { ResolvedWithInspectionId = Guid.NewGuid() };
        var entered = Inspection(vehicleId, 1, defects: forged);
        Assert.Null(entered.FindDefect("Mirror")!.ResolvedWithInspectionId);

        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        ResolveByHand(addressed, "Mirror");
        Cascade(addressed, "Mirror", Unit, entered);

        // Amend re-states the DVIR (severity corrected) — never the maintenance history.
        Assert.True(TestInspections.AmendWith(
            entered, [Defect("Mirror", InspectionDefectSeverity.Minor)], unit: Unit).IsSuccess);

        var mirror = entered.FindDefect("Mirror")!;
        Assert.Equal(InspectionDefectSeverity.Minor, mirror.Severity);
        Assert.True(mirror.IsResolved);
        Assert.Equal(addressed.Id, mirror.ResolvedWithInspectionId);
    }
}

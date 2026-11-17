using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The duplicate-defect rule: a known fault re-reported on every DVIR until it is fixed leaves
/// one open defect per inspection for one component, and REPAIRING it clears the reports made
/// before the repair — same vehicle (the panel's scope), same component (trimmed,
/// case-insensitive), still open, on another inspection. Only a repair cascades.
/// </summary>
public class DuplicateDefectsTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 11, 2, 7, 0, 0, TimeSpan.Zero);

    // Day 3 (Nov 5 07:00) is the newest report BEFORE this; day 4 is after it.
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

    private static void RepairByHand(VehicleInspection inspection, string item, string? note = "replaced at Thompson") =>
        Assert.True(inspection
            .ResolveDefect(item, DefectResolutionReason.PreviouslyRepaired, note, "R. Ballantyne", ResolvedAt)
            .IsSuccess);

    private static DefectVehicleScope VehicleOf(VehicleInspection inspection, string? currentUnit = Unit) =>
        DefectVehicleScope.ForVehicle(inspection.VehicleId!.Value, currentUnit);

    private static IReadOnlyList<DefectOnInspection> Cascade(
        VehicleInspection source,
        string item,
        DefectVehicleScope scope,
        params VehicleInspection[] candidates) =>
        DuplicateDefects.Resolve(source, item, scope, ResolvedAt, candidates);

    // ---- F1: only a repair cascades ----

    [Theory]
    [InlineData(DefectResolutionReason.PreviouslyRepaired, true)]
    [InlineData(DefectResolutionReason.RepairedUnderWorkOrder, true)]
    [InlineData(DefectResolutionReason.ReportedInError, false)]
    [InlineData(DefectResolutionReason.AcceptedMonitoring, false)]
    [InlineData(DefectResolutionReason.NoFaultFound, false)]
    public void Only_a_repair_clears_duplicates(DefectResolutionReason reason, bool clears) =>
        Assert.Equal(clears, reason.ClearsDuplicates());

    [Fact]
    public void Every_reason_has_a_decided_duplicate_rule()
    {
        // A new reason must be placed on one side of the rule deliberately, not by default.
        Assert.Equal(
            [DefectResolutionReason.RepairedUnderWorkOrder, DefectResolutionReason.PreviouslyRepaired],
            Enum.GetValues<DefectResolutionReason>().Where(r => r.ClearsDuplicates()));
    }

    [Theory]
    [InlineData(DefectResolutionReason.ReportedInError)]
    [InlineData(DefectResolutionReason.AcceptedMonitoring)]
    public void A_judgement_about_one_report_clears_nothing_and_is_refused_by_the_domain(DefectResolutionReason reason)
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var duplicate = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        Assert.True(addressed.ResolveDefect("Mirror", reason, null, "Dispatch", ResolvedAt).IsSuccess);

        Assert.Throws<InvalidOperationException>(() => Cascade(addressed, "Mirror", VehicleOf(addressed), duplicate));
        Assert.Throws<ArgumentException>(() => duplicate.ResolveAsDuplicateOf(addressed.Id, addressed.FindDefect("Mirror")!));
        Assert.False(duplicate.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void No_fault_found_under_a_work_order_is_refused_too()
    {
        var vehicleId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var duplicate = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        Assert.True(addressed.AssignDefectToWorkOrder("Mirror", workOrderId, false).IsSuccess);
        Assert.True(addressed.ResolveDefectUnderWorkOrder(
            "Mirror", workOrderId, DefectResolutionReason.NoFaultFound, "fine on the bench", "M. Cardinal", ResolvedAt));

        Assert.Throws<InvalidOperationException>(() => Cascade(addressed, "Mirror", VehicleOf(addressed), duplicate));
        Assert.False(duplicate.FindDefect("Mirror")!.IsResolved);
    }

    // ---- The rule ----

    [Fact]
    public void A_repair_clears_the_same_component_on_every_earlier_inspection_of_the_vehicle()
    {
        var vehicleId = Guid.NewGuid();
        var day1 = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        var day2 = Inspection(vehicleId, 2, defects: [Defect("Mirror"), Defect("Wipers")]);
        var day3 = Inspection(vehicleId, 3, defects: Defect("Mirror", InspectionDefectSeverity.Minor));

        // The dispatcher clears the NEWEST report; the older two are its duplicates.
        RepairByHand(day3, "Mirror");
        var cleared = Cascade(day3, "Mirror", VehicleOf(day3), day3, day2, day1);

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

        Assert.Null(addressed.ResolvedWithInspectionId);
        Assert.False(day2.FindDefect("Wipers")!.IsResolved);
    }

    [Fact]
    public void A_report_made_at_or_after_the_repair_stays_open()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        var before = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var atTheInstant = TestInspections.PreTrip(
            vehicleId: vehicleId, defects: [Defect("Mirror")], performedAt: ResolvedAt, tripNumber: "TR-AT");
        var after = Inspection(vehicleId, 4, defects: Defect("Mirror"));

        RepairByHand(addressed, "Mirror");
        var cleared = Cascade(addressed, "Mirror", VehicleOf(addressed), after, atTheInstant, before);

        Assert.Equal(before.Id, Assert.Single(cleared).Inspection.Id);
        Assert.False(atTheInstant.FindDefect("Mirror")!.IsResolved);
        Assert.False(after.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void Find_reports_exactly_what_Resolve_then_clears_and_changes_nothing()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 3, defects: Defect("Mirror"));
        var day1 = Inspection(vehicleId, 1, defects: Defect("mirror"));
        var day2 = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var later = Inspection(vehicleId, 4, defects: Defect("Mirror"));
        var candidates = new[] { later, day2, addressed, day1 };
        foreach (var c in candidates)
        {
            c.ClearDomainEvents();
        }

        // Previewed while the addressed defect is still open.
        var found = DuplicateDefects.Find(addressed, "Mirror", VehicleOf(addressed), ResolvedAt, candidates);

        Assert.Equal([day1.Id, day2.Id], found.Select(f => f.Inspection.Id));
        Assert.All(candidates, c => Assert.Empty(c.DomainEvents));
        Assert.All(found, f => Assert.False(f.Defect.IsResolved));

        RepairByHand(addressed, "Mirror");
        var cleared = Cascade(addressed, "Mirror", VehicleOf(addressed), candidates);

        Assert.Equal(found.Select(f => (f.Inspection.Id, f.Defect.Item)), cleared.Select(c => (c.Inspection.Id, c.Defect.Item)));
    }

    [Fact]
    public void The_include_filter_narrows_but_cannot_widen()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 3, defects: Defect("Mirror"));
        var day1 = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        var day2 = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var otherTruck = Inspection(Guid.NewGuid(), 1, unit: "U-07", defects: Defect("Mirror"));

        RepairByHand(addressed, "Mirror");
        var cleared = DuplicateDefects.Resolve(
            addressed, "Mirror", VehicleOf(addressed), ResolvedAt, [day1, day2, otherTruck],
            include: _ => true);
        Assert.Equal([day1.Id, day2.Id], cleared.Select(c => c.Inspection.Id));
        Assert.False(otherTruck.FindDefect("Mirror")!.IsResolved);

        var vehicle2 = Guid.NewGuid();
        var addressed2 = Inspection(vehicle2, 3, defects: Defect("Mirror"));
        var a = Inspection(vehicle2, 1, defects: Defect("Mirror"));
        var b = Inspection(vehicle2, 2, defects: Defect("Mirror"));
        RepairByHand(addressed2, "Mirror");
        var narrowed = DuplicateDefects.Resolve(
            addressed2, "Mirror", VehicleOf(addressed2), ResolvedAt, [a, b],
            include: d => d.Inspection.Id == b.Id);
        Assert.Equal(b.Id, Assert.Single(narrowed).Inspection.Id);
        Assert.False(a.FindDefect("Mirror")!.IsResolved);
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

        RepairByHand(addressed, "Mirror");
        Cascade(addressed, "Mirror", VehicleOf(addressed), duplicate, unrelated);

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

        RepairByHand(addressed, "Mirror");
        var cleared = Cascade(addressed, "Mirror", VehicleOf(addressed), earlier);

        Assert.Empty(cleared);
        var mirror = earlier.FindDefect("Mirror")!;
        Assert.Equal(DefectResolutionReason.ReportedInError, mirror.ResolutionReason);
        Assert.Equal("glare, not a crack", mirror.ResolutionNote);
        Assert.Equal(Day1, mirror.ResolvedAtUtc);
        Assert.Null(mirror.ResolvedWithInspectionId);
        Assert.Empty(earlier.DomainEvents);
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

        RepairByHand(addressed, "Mirror");

        Assert.Empty(Cascade(addressed, "Mirror", VehicleOf(addressed), foreign));
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

        RepairByHand(addressed, "Mirror");
        var cleared = Assert.Single(Cascade(addressed, "Mirror", VehicleOf(addressed), duplicate));

        Assert.Equal(duplicate.Id, cleared.Inspection.Id);
        // The duplicate keeps its own spelling — only the resolution stamp is copied.
        Assert.Equal(variant, cleared.Defect.Item);
        Assert.True(duplicate.FindDefect(variant)!.IsResolved);
    }

    [Theory]
    [InlineData("Mirror", "  mirror\t", true)]
    [InlineData("Mirror", "MIRROR", true)]
    [InlineData("Mirror", "Mirror bracket", false)]
    [InlineData(null, "", true)]
    [InlineData("Mirror", null, false)]
    public void The_item_rule_is_one_public_function(string? left, string? right, bool same) =>
        Assert.Equal(same, VehicleInspection.IsSameItem(left, right));

    [Fact]
    public void A_similar_but_different_component_is_not_a_duplicate()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var other = Inspection(vehicleId, 1, defects: Defect("Mirror bracket"));

        RepairByHand(addressed, "Mirror");

        Assert.Empty(Cascade(addressed, "Mirror", VehicleOf(addressed), other));
        Assert.False(other.FindDefect("Mirror bracket")!.IsResolved);
    }

    // ---- F5: the scope is exactly the panel's rule ----

    [Fact]
    public void Another_vehicles_defect_for_the_same_component_is_untouched()
    {
        var addressed = Inspection(Guid.NewGuid(), 2, defects: Defect("Mirror"));
        var otherTruck = Inspection(Guid.NewGuid(), 1, unit: "U-07", defects: Defect("Mirror"));
        var otherTruckSameUnitText = Inspection(Guid.NewGuid(), 1, unit: Unit, defects: Defect("Mirror"));

        RepairByHand(addressed, "Mirror");
        var cleared = Cascade(addressed, "Mirror", VehicleOf(addressed), otherTruck, otherTruckSameUnitText);

        // A LINKED inspection of another vehicle never matches on unit text: the link wins.
        Assert.Empty(cleared);
        Assert.False(otherTruck.FindDefect("Mirror")!.IsResolved);
        Assert.False(otherTruckSameUnitText.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void Legacy_unit_only_inspections_of_the_vehicle_match_on_its_current_unit_number()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 3, unit: "U-04 (old)", defects: Defect("Mirror"));
        var legacy = Inspection(vehicleId: null, 1, unit: Unit, defects: Defect("Mirror"));
        var legacyOtherUnit = Inspection(vehicleId: null, 2, unit: "U-07", defects: Defect("Mirror"));

        RepairByHand(addressed, "Mirror");
        // The vehicle's CURRENT unit number is U-04 — the panel's rule, not the inspection's text.
        var cleared = Cascade(addressed, "Mirror", VehicleOf(addressed, "U-04"), legacy, legacyOtherUnit);

        Assert.Equal(legacy.Id, Assert.Single(cleared).Inspection.Id);
        Assert.False(legacyOtherUnit.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void With_the_vehicles_unit_number_unknown_only_the_id_matches()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 3, defects: Defect("Mirror"));
        var sameVehicle = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        var legacy = Inspection(vehicleId: null, 2, unit: Unit, defects: Defect("Mirror"));

        RepairByHand(addressed, "Mirror");
        var cleared = Cascade(addressed, "Mirror", VehicleOf(addressed, currentUnit: null), sameVehicle, legacy);

        Assert.Equal(sameVehicle.Id, Assert.Single(cleared).Inspection.Id);
        Assert.False(legacy.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void A_unit_only_scope_matches_unit_only_inspections_with_that_unit_and_nothing_linked()
    {
        var addressed = Inspection(vehicleId: null, 3, defects: Defect("Mirror"));
        var legacy = Inspection(vehicleId: null, 1, defects: Defect("Mirror"));
        var linkedSameUnitText = Inspection(Guid.NewGuid(), 2, defects: Defect("Mirror"));
        var otherUnit = Inspection(vehicleId: null, 2, unit: "U-07", defects: Defect("Mirror"));

        RepairByHand(addressed, "Mirror");
        var cleared = Cascade(addressed, "Mirror", DefectVehicleScope.UnitOnly(Unit), legacy, linkedSameUnitText, otherUnit);

        // An inspection linked to a vehicle is that vehicle's, whatever its unit text says.
        Assert.Equal(legacy.Id, Assert.Single(cleared).Inspection.Id);
        Assert.False(linkedSameUnitText.FindDefect("Mirror")!.IsResolved);
        Assert.False(otherUnit.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void Scope_membership_matches_the_panels_predicate()
    {
        var v = Guid.NewGuid();
        var linked = Inspection(v, 1, unit: "anything");
        var legacySameUnit = Inspection(null, 1, unit: "U-04");
        var legacyOtherUnit = Inspection(null, 1, unit: "U-07");
        var otherVehicleSameUnit = Inspection(Guid.NewGuid(), 1, unit: "U-04");

        var forVehicle = DefectVehicleScope.ForVehicle(v, "U-04");
        Assert.Equal(
            [true, true, false, false],
            new[] { linked, legacySameUnit, legacyOtherUnit, otherVehicleSameUnit }.Select(forVehicle.Includes));

        var idOnly = DefectVehicleScope.ForVehicle(v, "  ");
        Assert.Null(idOnly.Unit);
        Assert.Equal(
            [true, false, false, false],
            new[] { linked, legacySameUnit, legacyOtherUnit, otherVehicleSameUnit }.Select(idOnly.Includes));

        var unitOnly = DefectVehicleScope.UnitOnly("U-04");
        Assert.Equal(
            [false, true, false, false],
            new[] { linked, legacySameUnit, legacyOtherUnit, otherVehicleSameUnit }.Select(unitOnly.Includes));
    }

    // ---- Work-order link and provenance ----

    [Fact]
    public void A_duplicate_on_another_active_work_order_is_resolved_but_stays_on_that_work_order()
    {
        var vehicleId = Guid.NewGuid();
        var otherWorkOrder = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var attached = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        Assert.True(attached.AssignDefectToWorkOrder("Mirror", otherWorkOrder, generatedWorkOrderIsOpen: false).IsSuccess);

        RepairByHand(addressed, "Mirror");
        Assert.Single(Cascade(addressed, "Mirror", VehicleOf(addressed), attached));

        var mirror = attached.FindDefect("Mirror")!;
        Assert.True(mirror.IsResolved);
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
        Assert.Single(Cascade(addressed, "Mirror", VehicleOf(addressed), duplicate));

        var mirror = duplicate.FindDefect("Mirror")!;
        Assert.Equal(workOrderId, mirror.ResolvedByWorkOrderId);
        Assert.Null(mirror.WorkOrderId);
    }

    [Fact]
    public void A_duplicate_held_by_an_open_legacy_generated_work_order_is_resolved_and_no_longer_held()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var legacyHeld = Inspection(vehicleId, 1, defects: Defect("Mirror"));
        Assert.True(legacyHeld.LinkWorkOrder(Guid.NewGuid()).IsSuccess);

        RepairByHand(addressed, "Mirror");
        Assert.Single(Cascade(addressed, "Mirror", VehicleOf(addressed), legacyHeld));

        Assert.True(legacyHeld.FindDefect("Mirror")!.IsResolved);
        Assert.Equal(
            InspectionErrors.DefectAlreadyResolved,
            legacyHeld.CanAttachDefect("Mirror", generatedWorkOrderIsOpen: true).Error);
    }

    [Fact]
    public void Resolving_duplicates_of_an_unresolved_defect_is_refused()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Inspection(vehicleId, 2, defects: Defect("Mirror"));
        var duplicate = Inspection(vehicleId, 1, defects: Defect("Mirror"));

        Assert.Throws<InvalidOperationException>(() => Cascade(addressed, "Mirror", VehicleOf(addressed), duplicate));
        Assert.Throws<ArgumentException>(() => duplicate.ResolveAsDuplicateOf(addressed.Id, addressed.FindDefect("Mirror")!));
        Assert.False(duplicate.FindDefect("Mirror")!.IsResolved);
    }

    [Fact]
    public void An_inspection_is_never_a_duplicate_of_itself()
    {
        var addressed = Inspection(Guid.NewGuid(), 1, defects: Defect("Mirror"));
        RepairByHand(addressed, "Mirror");
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
        RepairByHand(addressed, "Mirror");
        Cascade(addressed, "Mirror", VehicleOf(addressed), entered);

        Assert.True(TestInspections.AmendWith(
            entered, [Defect("Mirror", InspectionDefectSeverity.Minor)], unit: Unit).IsSuccess);

        var mirror = entered.FindDefect("Mirror")!;
        Assert.Equal(InspectionDefectSeverity.Minor, mirror.Severity);
        Assert.True(mirror.IsResolved);
        Assert.Equal(addressed.Id, mirror.ResolvedWithInspectionId);
    }

    // ---- F4: the repair cutoff ----

    [Fact]
    public void A_date_only_service_date_cuts_off_at_the_start_of_that_day_in_winnipeg_winter()
    {
        // "2026-01-15" as the Dispatcher sends it, parsed at midnight in any offset.
        foreach (var offset in new[] { TimeSpan.Zero, TimeSpan.FromHours(-6), TimeSpan.FromHours(5) })
        {
            var cutoff = DefectRepairCutoff.ForServiceDate(
                new DateTimeOffset(2026, 1, 15, 0, 0, 0, offset), completedAtUtc: DateTimeOffset.MaxValue);

            // CST is UTC-6: local midnight is 06:00Z.
            Assert.Equal(new DateTimeOffset(2026, 1, 15, 6, 0, 0, TimeSpan.Zero), cutoff);
        }
    }

    [Fact]
    public void A_date_only_service_date_in_summer_uses_central_daylight_time()
    {
        var cutoff = DefectRepairCutoff.ForServiceDate(
            new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero), DateTimeOffset.MaxValue);

        Assert.Equal(new DateTimeOffset(2026, 7, 15, 5, 0, 0, TimeSpan.Zero), cutoff);
    }

    [Fact]
    public void A_service_instant_is_placed_on_its_winnipeg_day()
    {
        // 02:30Z on Jan 16 is 20:30 on Jan 15 in Winnipeg — the repair day is the 15th.
        var cutoff = DefectRepairCutoff.ForServiceDate(
            new DateTimeOffset(2026, 1, 16, 2, 30, 0, TimeSpan.Zero), DateTimeOffset.MaxValue);

        Assert.Equal(new DateTimeOffset(2026, 1, 15, 6, 0, 0, TimeSpan.Zero), cutoff);
    }

    [Fact]
    public void A_future_service_date_never_cuts_off_after_the_completion()
    {
        var completedAt = new DateTimeOffset(2026, 1, 10, 18, 0, 0, TimeSpan.Zero);

        var cutoff = DefectRepairCutoff.ForServiceDate(new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero), completedAt);

        Assert.Equal(completedAt, cutoff);
    }
}

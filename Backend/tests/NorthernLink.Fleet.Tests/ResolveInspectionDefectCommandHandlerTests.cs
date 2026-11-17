using Microsoft.Extensions.Logging;
using NorthernLink.Fleet.Application.Inspections;
using NorthernLink.Fleet.Application.Inspections.ResolveDefect;
using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// Handler wiring for clearing one defect: the aggregate owns the addressing and the
/// already-resolved rule, so this proves that the inspection is loaded, the failure paths save
/// nothing, the attribution/timestamp defaults land where they should — and the two-phase
/// duplicate clearing: only a repair, only the reports the dispatcher listed that still qualify,
/// in a second save that retries conflicts and can never fail the call.
/// </summary>
public class ResolveInspectionDefectCommandHandlerTests
{
    private static ResolveInspectionDefectCommand Command(
        Guid inspectionId,
        string item = "Brakes",
        string? resolvedBy = "R. Ballantyne",
        DefectResolutionReason reason = DefectResolutionReason.PreviouslyRepaired,
        params VehicleInspection[] clear) =>
        new(
            TestVehicles.TenantId,
            inspectionId,
            item,
            reason,
            Note: "Fixed at Thompson",
            ResolvedBy: resolvedBy,
            ClearDuplicates: clear.Length == 0
                ? null
                : [.. clear.Select(i => new DuplicateDefectReference(i.Id, i.Defects.Single().Item))]);

    private sealed class Fixture
    {
        public InMemoryVehicleInspectionRepository Repository { get; } = new();
        public InMemoryVehicleRepository Vehicles { get; } = new();
        public CapturingLogger<DuplicateDefectCascade> Logger { get; } = new();

        public ResolveInspectionDefectCommandHandler Handler =>
            new(Repository, TestDuplicateCascade.Over(Repository, Vehicles, Logger));

        public Fixture(params VehicleInspection[] stored)
        {
            foreach (var inspection in stored)
            {
                Repository.Add(inspection);
            }
        }
    }

    [Fact]
    public async Task An_unknown_inspection_fails_with_not_found_and_saves_nothing()
    {
        var f = new Fixture();

        var result = await f.Handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.NotFound, result.Error);
        Assert.Equal(0, f.Repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Resolving_stamps_the_defect_and_saves_once()
    {
        var inspection = TestInspections.PreTrip(defects: [
            new InspectionDefect { Item = "Brakes", Severity = InspectionDefectSeverity.Major },
        ]);
        var f = new Fixture(inspection);
        var before = DateTimeOffset.UtcNow;

        var result = await f.Handler.Handle(Command(inspection.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, f.Repository.SaveChangesCallCount);
        // No duplicates requested: phase 2 never runs.
        Assert.Equal(0, f.Repository.TrySaveAttemptCount);
        Assert.Equal(0, f.Repository.GetWithOpenDefectsCallCount);

        var brakes = Assert.Single(inspection.Defects);
        Assert.Equal(DefectResolutionReason.PreviouslyRepaired, brakes.ResolutionReason);
        Assert.Equal("Fixed at Thompson", brakes.ResolutionNote);
        Assert.Equal("R. Ballantyne", brakes.ResolvedBy);
        Assert.InRange(brakes.ResolvedAtUtc!.Value, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task A_resolve_with_no_name_on_it_is_attributed_to_dispatch()
    {
        var inspection = TestInspections.PreTrip(defects: [
            new InspectionDefect { Item = "Brakes", Severity = InspectionDefectSeverity.Major },
        ]);
        var f = new Fixture(inspection);

        var result = await f.Handler.Handle(Command(inspection.Id, resolvedBy: "   "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dispatch", Assert.Single(inspection.Defects).ResolvedBy);
    }

    [Fact]
    public async Task An_unknown_item_fails_with_defect_not_found_and_saves_nothing()
    {
        var inspection = TestInspections.PreTrip(defects: [
            new InspectionDefect { Item = "Brakes", Severity = InspectionDefectSeverity.Major },
        ]);
        var f = new Fixture(inspection);

        var result = await f.Handler.Handle(Command(inspection.Id, item: "Headlights"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.DefectNotFound, result.Error);
        Assert.Equal(0, f.Repository.SaveChangesCallCount);
    }

    // ---- Duplicates ----

    private static readonly DateTimeOffset Day1 = DateTimeOffset.UtcNow.AddDays(-10);

    private static VehicleInspection Daily(Guid? vehicleId, int day, string item, string unit = "U-04") =>
        TestInspections.PreTrip(
            vehicleId: vehicleId,
            unit: unit,
            defects: [new InspectionDefect { Item = item, Severity = InspectionDefectSeverity.Major }],
            performedAt: Day1.AddDays(day),
            tripNumber: $"TR-{4800 + day}");

    [Fact]
    public async Task A_repair_clears_the_listed_duplicates_in_a_second_save_and_lists_the_addressed_defect_first()
    {
        var vehicleId = Guid.NewGuid();
        var day1 = Daily(vehicleId, 1, "Mirror");
        var day2 = Daily(vehicleId, 2, "mirror ");
        var day3 = Daily(vehicleId, 3, "Mirror");
        var f = new Fixture(day3, day1, day2);

        var result = await f.Handler.Handle(
            Command(day2.Id, item: "MIRROR", clear: [day3, day1]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Phase 1 and phase 2 are separate saves.
        Assert.Equal(1, f.Repository.SaveChangesCallCount);
        Assert.Equal(1, f.Repository.TrySaveChangesCallCount);
        Assert.Equal(1, f.Repository.GetWithOpenDefectsCallCount);

        // Addressed first, then the duplicates oldest report first.
        Assert.Equal(
            [(day2.Id, "mirror ", "TR-4802"), (day1.Id, "Mirror", "TR-4801"), (day3.Id, "Mirror", "TR-4803")],
            result.Value.Resolved.Select(r => (r.InspectionId, r.Item, r.TripNumber)));

        var addressed = day2.Defects.Single();
        Assert.Null(addressed.ResolvedWithInspectionId);
        foreach (var duplicate in new[] { day1, day3 }.Select(i => i.Defects.Single()))
        {
            Assert.Equal(day2.Id, duplicate.ResolvedWithInspectionId);
            Assert.Equal(addressed.ResolvedAtUtc, duplicate.ResolvedAtUtc);
            Assert.Equal("R. Ballantyne", duplicate.ResolvedBy);
            Assert.Equal(DefectResolutionReason.PreviouslyRepaired, duplicate.ResolutionReason);
        }
    }

    [Fact]
    public async Task Without_a_list_a_repair_clears_no_duplicates()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 2, "Mirror");
        var duplicate = Daily(vehicleId, 1, "Mirror");
        var f = new Fixture(addressed, duplicate);

        var result = await f.Handler.Handle(Command(addressed.Id, item: "Mirror"), CancellationToken.None);

        Assert.Equal(addressed.Id, Assert.Single(result.Value.Resolved).InspectionId);
        Assert.False(duplicate.Defects.Single().IsResolved);
        Assert.Equal(0, f.Repository.GetWithOpenDefectsCallCount);
    }

    [Fact]
    public async Task Only_listed_reports_that_are_still_valid_duplicates_are_cleared()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 3, "Mirror");
        var shownAndValid = Daily(vehicleId, 1, "Mirror");
        var notShown = Daily(vehicleId, 2, "Mirror");
        var otherTruck = Daily(Guid.NewGuid(), 1, "Mirror", unit: "U-07");
        var alreadyCleared = Daily(vehicleId, 1, "Mirror");
        Assert.True(alreadyCleared
            .ResolveDefect("Mirror", DefectResolutionReason.ReportedInError, "glare", "Dispatch", Day1)
            .IsSuccess);
        var differentComponent = Daily(vehicleId, 1, "Horn");
        var f = new Fixture(addressed, shownAndValid, notShown, otherTruck, alreadyCleared, differentComponent);

        var command = Command(addressed.Id, item: "Mirror", clear: [shownAndValid, otherTruck, alreadyCleared, differentComponent])
            with
            {
                ClearDuplicates =
                [
                    new(shownAndValid.Id, "  MIRROR "),          // the domain's item rule matches it
                    new(otherTruck.Id, "Mirror"),                // another vehicle — not a duplicate
                    new(alreadyCleared.Id, "Mirror"),            // resolved meanwhile — skipped
                    new(differentComponent.Id, "Horn"),          // a different component
                    new(Guid.NewGuid(), "Mirror"),               // gone
                ],
            };

        var result = await f.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([addressed.Id, shownAndValid.Id], result.Value.Resolved.Select(r => r.InspectionId));
        Assert.False(notShown.Defects.Single().IsResolved);
        Assert.False(otherTruck.Defects.Single().IsResolved);
        Assert.False(differentComponent.Defects.Single().IsResolved);
        Assert.Equal(DefectResolutionReason.ReportedInError, alreadyCleared.Defects.Single().ResolutionReason);
    }

    [Fact]
    public async Task A_listed_report_made_after_the_resolution_is_not_cleared()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 1, "Mirror");
        var futureDated = TestInspections.PreTrip(
            vehicleId: vehicleId,
            defects: [new InspectionDefect { Item = "Mirror", Severity = InspectionDefectSeverity.Major }],
            performedAt: DateTimeOffset.UtcNow.AddHours(2),
            tripNumber: "TR-LATER");
        var f = new Fixture(addressed, futureDated);

        var result = await f.Handler.Handle(
            Command(addressed.Id, item: "Mirror", clear: [futureDated]), CancellationToken.None);

        Assert.Single(result.Value.Resolved);
        Assert.False(futureDated.Defects.Single().IsResolved);
    }

    [Theory]
    [InlineData(DefectResolutionReason.ReportedInError)]
    [InlineData(DefectResolutionReason.AcceptedMonitoring)]
    public async Task A_non_repair_reason_with_duplicates_listed_is_a_400_and_nothing_is_saved(DefectResolutionReason reason)
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 2, "Mirror");
        var duplicate = Daily(vehicleId, 1, "Mirror");
        var f = new Fixture(addressed, duplicate);

        var result = await f.Handler.Handle(
            Command(addressed.Id, item: "Mirror", reason: reason, clear: [duplicate]), CancellationToken.None);

        Assert.Equal(InspectionErrors.DuplicatesOnlyClearedByRepair, result.Error);
        Assert.Equal(NorthernLink.Shared.Kernel.ErrorType.Validation, result.Error.Type);
        Assert.All(new[] { addressed, duplicate }, i => Assert.False(i.Defects.Single().IsResolved));
        Assert.Equal(0, f.Repository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(DefectResolutionReason.ReportedInError)]
    [InlineData(DefectResolutionReason.AcceptedMonitoring)]
    public async Task A_non_repair_reason_with_an_empty_list_resolves_just_that_report(DefectResolutionReason reason)
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 2, "Mirror");
        var duplicate = Daily(vehicleId, 1, "Mirror");
        var f = new Fixture(addressed, duplicate);

        var result = await f.Handler.Handle(
            Command(addressed.Id, item: "Mirror", reason: reason) with { ClearDuplicates = [] },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Resolved);
        Assert.False(duplicate.Defects.Single().IsResolved);
        Assert.Equal(0, f.Repository.GetWithOpenDefectsCallCount);
    }

    [Fact]
    public async Task A_lost_version_race_is_retried_on_fresh_state_and_the_response_lists_only_what_was_cleared()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 3, "Mirror");
        var day1 = Daily(vehicleId, 1, "Mirror");
        var day2 = Daily(vehicleId, 2, "Mirror");
        var f = new Fixture(addressed, day1, day2);
        f.Repository.ConflictsBeforeSave = 1;
        // The writer that won: it cleared day1 by hand between our two attempts.
        f.Repository.OnConflict = _ => Assert.True(day1
            .ResolveDefect("Mirror", DefectResolutionReason.AcceptedMonitoring, "watching", "Other dispatcher", DateTimeOffset.UtcNow)
            .IsSuccess);

        var result = await f.Handler.Handle(
            Command(addressed.Id, item: "Mirror", clear: [day1, day2]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, f.Repository.TrySaveAttemptCount);
        Assert.Equal(1, f.Repository.TrySaveChangesCallCount);
        Assert.Equal(2, f.Repository.GetWithOpenDefectsCallCount);
        Assert.Equal([addressed.Id, day2.Id], result.Value.Resolved.Select(r => r.InspectionId));
        Assert.Equal(DefectResolutionReason.AcceptedMonitoring, day1.Defects.Single().ResolutionReason);
        Assert.Null(day1.Defects.Single().ResolvedWithInspectionId);
        Assert.Equal(addressed.Id, day2.Defects.Single().ResolvedWithInspectionId);
        Assert.DoesNotContain(f.Logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Exhausted_retries_leave_the_duplicates_open_log_a_warning_and_still_succeed()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 3, "Mirror");
        var duplicate = Daily(vehicleId, 1, "Mirror");
        var f = new Fixture(addressed, duplicate);
        f.Repository.ConflictsBeforeSave = DuplicateDefectCascade.MaxAttempts;

        var result = await f.Handler.Handle(
            Command(addressed.Id, item: "Mirror", clear: [duplicate]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DuplicateDefectCascade.MaxAttempts, f.Repository.TrySaveAttemptCount);
        Assert.Equal(0, f.Repository.TrySaveChangesCallCount);
        // The primary write stands; the duplicate is left open (the safe direction).
        Assert.True(addressed.Defects.Single().IsResolved);
        Assert.False(duplicate.Defects.Single().IsResolved);
        Assert.Equal(addressed.Id, Assert.Single(result.Value.Resolved).InspectionId);

        var warning = Assert.Single(f.Logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(addressed.Id.ToString(), warning.Message);
        Assert.Contains(duplicate.Id.ToString(), warning.Message);
    }

    [Fact]
    public async Task Any_other_cascade_failure_is_discarded_and_logged_and_the_call_still_succeeds()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 3, "Mirror");
        var duplicate = Daily(vehicleId, 1, "Mirror");
        var f = new Fixture(addressed, duplicate);
        f.Repository.ThrowOnTrySave = new InvalidOperationException("connection reset");

        var result = await f.Handler.Handle(
            Command(addressed.Id, item: "Mirror", clear: [duplicate]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Resolved);
        Assert.Equal(1, f.Repository.DiscardChangesCallCount);
        Assert.False(duplicate.Defects.Single().IsResolved);
        Assert.Single(f.Logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Legacy_unit_only_duplicates_are_found_through_the_vehicles_current_unit_number()
    {
        var vehicle = TestVehicles.Create(unitNumber: "U-04");
        var linked = Daily(vehicle.Id, 2, "Mirror", unit: "U-04 (renamed since)");
        var legacy = Daily(vehicleId: null, 1, "Mirror", unit: "U-04");
        var f = new Fixture(linked, legacy);
        f.Vehicles.Add(vehicle);

        var result = await f.Handler.Handle(
            Command(linked.Id, item: "Mirror", clear: [legacy]), CancellationToken.None);

        Assert.Equal([linked.Id, legacy.Id], result.Value.Resolved.Select(r => r.InspectionId));
        Assert.Equal(linked.Id, legacy.Defects.Single().ResolvedWithInspectionId);
    }

    [Fact]
    public async Task A_renumbered_vehicle_keeps_its_old_unit_only_reports_out_of_its_scope()
    {
        // The truck was U-04 and is now U-14. Legacy unit-only DVIRs still say "U-04"; the panel
        // for the truck lists its linked DVIRs plus unit-only "U-14" ones — never "U-04".
        var vehicle = TestVehicles.Create(unitNumber: "U-14");
        var linked = Daily(vehicle.Id, 3, "Mirror", unit: "U-04");
        var legacyOldNumber = Daily(vehicleId: null, 1, "Mirror", unit: "U-04");
        var legacyNewNumber = Daily(vehicleId: null, 2, "Mirror", unit: "U-14");
        var f = new Fixture(linked, legacyOldNumber, legacyNewNumber);
        f.Vehicles.Add(vehicle);

        var fromLinked = await f.Handler.Handle(
            Command(linked.Id, item: "Mirror", clear: [legacyOldNumber, legacyNewNumber]), CancellationToken.None);

        Assert.Equal([linked.Id, legacyNewNumber.Id], fromLinked.Value.Resolved.Select(r => r.InspectionId));
        Assert.False(legacyOldNumber.Defects.Single().IsResolved);
    }

    [Fact]
    public async Task A_unit_only_source_no_vehicle_answers_to_reaches_only_unit_only_reports_of_that_unit()
    {
        // Nothing is numbered U-04 any more, so a "U-04" legacy report is scoped to its unit text
        // alone — a vehicle-linked report that happens to say "U-04" belongs to its vehicle.
        var renumbered = TestVehicles.Create(unitNumber: "U-14");
        var source = Daily(vehicleId: null, 3, "Mirror", unit: "U-04");
        var otherLegacy = Daily(vehicleId: null, 1, "Mirror", unit: "U-04");
        var linkedOldText = Daily(renumbered.Id, 2, "Mirror", unit: "U-04");
        var f = new Fixture(source, otherLegacy, linkedOldText);
        f.Vehicles.Add(renumbered);

        var result = await f.Handler.Handle(
            Command(source.Id, item: "Mirror", clear: [otherLegacy, linkedOldText]), CancellationToken.None);

        Assert.Equal([source.Id, otherLegacy.Id], result.Value.Resolved.Select(r => r.InspectionId));
        Assert.False(linkedOldText.Defects.Single().IsResolved);
    }

    [Fact]
    public async Task A_failed_resolve_clears_no_duplicates_and_saves_nothing()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 2, "Mirror");
        var duplicate = Daily(vehicleId, 1, "Mirror");
        Assert.True(addressed
            .ResolveDefect("Mirror", DefectResolutionReason.ReportedInError, null, "Dispatch", Day1)
            .IsSuccess);
        var f = new Fixture(addressed, duplicate);

        var result = await f.Handler.Handle(
            Command(addressed.Id, item: "Mirror", clear: [duplicate]), CancellationToken.None);

        Assert.Equal(InspectionErrors.DefectAlreadyResolved, result.Error);
        Assert.False(duplicate.Defects.Single().IsResolved);
        Assert.Equal(0, f.Repository.SaveChangesCallCount);
        Assert.Equal(0, f.Repository.GetWithOpenDefectsCallCount);
    }

    [Fact]
    public async Task A_reserved_reason_still_fails_before_any_duplicate_is_touched()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 2, "Mirror");
        var duplicate = Daily(vehicleId, 1, "Mirror");
        var f = new Fixture(addressed, duplicate);

        var result = await f.Handler.Handle(
            Command(addressed.Id, item: "Mirror", reason: DefectResolutionReason.RepairedUnderWorkOrder, clear: [duplicate]),
            CancellationToken.None);

        Assert.Equal(InspectionErrors.ResolutionReasonReservedForWorkOrder, result.Error);
        Assert.All(new[] { addressed, duplicate }, i => Assert.False(i.Defects.Single().IsResolved));
        Assert.Equal(0, f.Repository.SaveChangesCallCount);
    }
}

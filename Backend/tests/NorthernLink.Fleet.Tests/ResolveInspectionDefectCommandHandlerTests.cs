using NorthernLink.Fleet.Application.Inspections.ResolveDefect;
using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// Handler wiring for clearing one defect: the aggregate owns the addressing and the
/// already-resolved rule, so this proves only that the inspection is loaded, the failure paths
/// save nothing, and the attribution/timestamp defaults land where they should.
/// </summary>
public class ResolveInspectionDefectCommandHandlerTests
{
    private static ResolveInspectionDefectCommand Command(
        Guid inspectionId, string item = "Brakes", string? resolvedBy = "R. Ballantyne") =>
        new(
            TestVehicles.TenantId,
            inspectionId,
            item,
            DefectResolutionReason.PreviouslyRepaired,
            Note: "Fixed at Thompson",
            ResolvedBy: resolvedBy);

    private static (ResolveInspectionDefectCommandHandler Handler, InMemoryVehicleInspectionRepository Repository)
        Setup(params VehicleInspection[] stored)
    {
        var repository = new InMemoryVehicleInspectionRepository();
        foreach (var inspection in stored)
        {
            repository.Add(inspection);
        }

        return (new ResolveInspectionDefectCommandHandler(repository, new InMemoryVehicleRepository()), repository);
    }

    [Fact]
    public async Task An_unknown_inspection_fails_with_not_found_and_saves_nothing()
    {
        var (handler, repository) = Setup();

        var result = await handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.NotFound, result.Error);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Resolving_stamps_the_defect_and_saves_once()
    {
        var inspection = TestInspections.PreTrip(defects: [
            new InspectionDefect { Item = "Brakes", Severity = InspectionDefectSeverity.Major },
        ]);
        var (handler, repository) = Setup(inspection);
        var before = DateTimeOffset.UtcNow;

        var result = await handler.Handle(Command(inspection.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.SaveChangesCallCount);

        var brakes = Assert.Single(inspection.Defects);
        Assert.Equal(DefectResolutionReason.PreviouslyRepaired, brakes.ResolutionReason);
        Assert.Equal("Fixed at Thompson", brakes.ResolutionNote);
        Assert.Equal("R. Ballantyne", brakes.ResolvedBy);
        // Stamped server-side, never taken from the request body.
        Assert.InRange(brakes.ResolvedAtUtc!.Value, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task A_resolve_with_no_name_on_it_is_attributed_to_dispatch()
    {
        var inspection = TestInspections.PreTrip(defects: [
            new InspectionDefect { Item = "Brakes", Severity = InspectionDefectSeverity.Major },
        ]);
        var (handler, _) = Setup(inspection);

        var result = await handler.Handle(
            Command(inspection.Id, resolvedBy: "   "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dispatch", Assert.Single(inspection.Defects).ResolvedBy);
    }

    [Fact]
    public async Task An_unknown_item_fails_with_defect_not_found_and_saves_nothing()
    {
        var inspection = TestInspections.PreTrip(defects: [
            new InspectionDefect { Item = "Brakes", Severity = InspectionDefectSeverity.Major },
        ]);
        var (handler, repository) = Setup(inspection);

        var result = await handler.Handle(
            Command(inspection.Id, item: "Headlights"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.DefectNotFound, result.Error);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    // ---- Duplicates: the same component, still open, on the vehicle's other inspections ----

    private static readonly DateTimeOffset Day1 = new(2026, 11, 2, 7, 0, 0, TimeSpan.Zero);

    private static VehicleInspection Daily(Guid? vehicleId, int day, string item, string unit = "U-04") =>
        TestInspections.PreTrip(
            vehicleId: vehicleId,
            unit: unit,
            defects: [new InspectionDefect { Item = item, Severity = InspectionDefectSeverity.Major }],
            performedAt: Day1.AddDays(day),
            tripNumber: $"TR-{4800 + day}");

    [Fact]
    public async Task Resolving_clears_every_duplicate_in_one_save_and_lists_the_addressed_defect_first()
    {
        var vehicleId = Guid.NewGuid();
        var day1 = Daily(vehicleId, 1, "Mirror");
        var day2 = Daily(vehicleId, 2, "mirror ");
        var day3 = Daily(vehicleId, 3, "Mirror");
        var otherTruck = Daily(Guid.NewGuid(), 1, "Mirror", unit: "U-07");
        // Stored out of order on purpose: the response order is by report date, not storage.
        var (handler, repository) = Setup(day3, day1, otherTruck, day2);

        var result = await handler.Handle(
            Command(day2.Id, item: "MIRROR"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.SaveChangesCallCount);

        // Addressed first, then the duplicates oldest report first.
        Assert.Equal(
            [(day2.Id, "mirror ", "TR-4802"), (day1.Id, "Mirror", "TR-4801"), (day3.Id, "Mirror", "TR-4803")],
            result.Value.Resolved.Select(r => (r.InspectionId, r.Item, r.TripNumber)));
        Assert.All(result.Value.Resolved, r => Assert.Equal("Major", r.Severity));

        var addressed = day2.Defects.Single();
        Assert.Null(addressed.ResolvedWithInspectionId);
        foreach (var duplicate in new[] { day1, day3 }.Select(i => i.Defects.Single()))
        {
            Assert.Equal(day2.Id, duplicate.ResolvedWithInspectionId);
            Assert.Equal(addressed.ResolvedAtUtc, duplicate.ResolvedAtUtc);
            Assert.Equal("R. Ballantyne", duplicate.ResolvedBy);
            Assert.Equal("Fixed at Thompson", duplicate.ResolutionNote);
            Assert.Equal(DefectResolutionReason.PreviouslyRepaired, duplicate.ResolutionReason);
            Assert.Null(duplicate.ResolvedByWorkOrderId);
        }

        Assert.False(otherTruck.Defects.Single().IsResolved);
    }

    [Fact]
    public async Task A_resolve_with_no_duplicates_returns_just_the_addressed_defect()
    {
        var inspection = Daily(Guid.NewGuid(), 1, "Brakes");
        var (handler, _) = Setup(inspection);

        var result = await handler.Handle(Command(inspection.Id), CancellationToken.None);

        var only = Assert.Single(result.Value.Resolved);
        Assert.Equal(new ResolvedDefectResponse(inspection.Id, "Brakes", "TR-4801", "Major"), only);
    }

    [Fact]
    public async Task Legacy_unit_only_duplicates_are_found_through_the_vehicles_current_unit_number()
    {
        var vehicle = TestVehicles.Create(unitNumber: "U-04");
        var vehicles = new InMemoryVehicleRepository();
        vehicles.Add(vehicle);
        var repository = new InMemoryVehicleInspectionRepository();
        var linked = Daily(vehicle.Id, 2, "Mirror", unit: "U-04 (renamed since)");
        var legacy = Daily(vehicleId: null, 1, "Mirror", unit: "U-04");
        repository.Add(linked);
        repository.Add(legacy);
        var handler = new ResolveInspectionDefectCommandHandler(repository, vehicles);

        var result = await handler.Handle(Command(linked.Id, item: "Mirror"), CancellationToken.None);

        Assert.Equal([linked.Id, legacy.Id], result.Value.Resolved.Select(r => r.InspectionId));
        Assert.Equal(linked.Id, legacy.Defects.Single().ResolvedWithInspectionId);
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
        var (handler, repository) = Setup(addressed, duplicate);

        var result = await handler.Handle(Command(addressed.Id, item: "Mirror"), CancellationToken.None);

        Assert.Equal(InspectionErrors.DefectAlreadyResolved, result.Error);
        Assert.False(duplicate.Defects.Single().IsResolved);
        Assert.Equal(0, repository.SaveChangesCallCount);
        Assert.Equal(0, repository.GetWithOpenDefectCallCount);
    }

    [Fact]
    public async Task A_reserved_reason_still_fails_before_any_duplicate_is_touched()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 2, "Mirror");
        var duplicate = Daily(vehicleId, 1, "Mirror");
        var (handler, repository) = Setup(addressed, duplicate);

        var result = await handler.Handle(
            Command(addressed.Id, item: "Mirror") with { Reason = DefectResolutionReason.NoFaultFound },
            CancellationToken.None);

        Assert.Equal(InspectionErrors.ResolutionReasonReservedForWorkOrder, result.Error);
        Assert.All(new[] { addressed, duplicate }, i => Assert.False(i.Defects.Single().IsResolved));
        Assert.Equal(0, repository.SaveChangesCallCount);
    }
}

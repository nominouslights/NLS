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

        return (new ResolveInspectionDefectCommandHandler(repository), repository);
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
}

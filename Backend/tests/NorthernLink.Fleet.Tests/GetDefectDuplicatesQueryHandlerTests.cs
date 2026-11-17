using NorthernLink.Fleet.Application.Inspections.GetDuplicates;
using NorthernLink.Fleet.Application.Inspections.ResolveDefect;
using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The duplicates preview: addressed like the resolve (same 404s), read-only, and — the point of
/// it — exactly the set a repair-type resolve clears when the dispatcher confirms all of it.
/// </summary>
public class GetDefectDuplicatesQueryHandlerTests
{
    private static readonly DateTimeOffset Day1 = DateTimeOffset.UtcNow.AddDays(-10);

    private static VehicleInspection Daily(
        Guid? vehicleId, int day, string item, string unit = "U-04",
        InspectionDefectSeverity severity = InspectionDefectSeverity.Major) =>
        TestInspections.PreTrip(
            vehicleId: vehicleId,
            unit: unit,
            defects: [new InspectionDefect { Item = item, Severity = severity }],
            performedAt: Day1.AddDays(day),
            tripNumber: $"TR-{4800 + day}");

    private static (GetDefectDuplicatesQueryHandler Preview, ResolveInspectionDefectCommandHandler Resolve, InMemoryVehicleInspectionRepository Repository)
        Setup(params VehicleInspection[] stored)
    {
        var repository = new InMemoryVehicleInspectionRepository();
        foreach (var inspection in stored)
        {
            repository.Add(inspection);
        }

        var cascade = TestDuplicateCascade.Over(repository);
        return (new GetDefectDuplicatesQueryHandler(repository, cascade),
            new ResolveInspectionDefectCommandHandler(repository, cascade),
            repository);
    }

    private static GetDefectDuplicatesQuery Query(Guid inspectionId, string item) =>
        new(TestVehicles.TenantId, inspectionId, item);

    [Fact]
    public async Task An_unknown_inspection_is_not_found()
    {
        var (preview, _, _) = Setup();

        var result = await preview.Handle(Query(Guid.NewGuid(), "Mirror"), CancellationToken.None);

        Assert.Equal(InspectionErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task An_unknown_item_is_not_found()
    {
        var addressed = Daily(Guid.NewGuid(), 1, "Mirror");
        var (preview, _, _) = Setup(addressed);

        var result = await preview.Handle(Query(addressed.Id, "Horn"), CancellationToken.None);

        Assert.Equal(InspectionErrors.DefectNotFound, result.Error);
    }

    [Fact]
    public async Task A_resolved_defect_has_no_duplicates_to_clear()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 2, "Mirror");
        var duplicate = Daily(vehicleId, 1, "Mirror");
        Assert.True(addressed
            .ResolveDefect("Mirror", DefectResolutionReason.ReportedInError, null, "Dispatch", DateTimeOffset.UtcNow)
            .IsSuccess);
        var (preview, _, repository) = Setup(addressed, duplicate);

        var result = await preview.Handle(Query(addressed.Id, "Mirror"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Duplicates);
        Assert.Equal(0, repository.GetWithOpenDefectsCallCount);
    }

    [Fact]
    public async Task Lists_the_open_duplicates_oldest_first_with_their_own_spelling_and_severity_and_writes_nothing()
    {
        var vehicleId = Guid.NewGuid();
        var addressed = Daily(vehicleId, 3, "Mirror");
        var day1 = Daily(vehicleId, 1, " mirror", severity: InspectionDefectSeverity.OutOfService);
        var day2 = Daily(vehicleId, 2, "MIRROR", severity: InspectionDefectSeverity.Minor);
        var otherTruck = Daily(Guid.NewGuid(), 1, "Mirror", unit: "U-07");
        var (preview, _, repository) = Setup(day2, addressed, otherTruck, day1);
        foreach (var i in repository.Inspections)
        {
            i.ClearDomainEvents();
        }

        var result = await preview.Handle(Query(addressed.Id, "  mirror "), CancellationToken.None);

        Assert.Equal(
            [
                new DefectDuplicateResponse(day1.Id, " mirror", "TR-4801", "OutOfService", day1.PerformedAt),
                new DefectDuplicateResponse(day2.Id, "MIRROR", "TR-4802", "Minor", day2.PerformedAt),
            ],
            result.Value.Duplicates);

        Assert.All(repository.Inspections, i => Assert.Empty(i.DomainEvents));
        Assert.All(repository.Inspections, i => Assert.False(i.Defects.Single().IsResolved));
        Assert.Equal(0, repository.SaveChangesCallCount + repository.TrySaveAttemptCount);
    }

    [Fact]
    public async Task Confirming_the_whole_preview_clears_exactly_the_previewed_reports()
    {
        var vehicle = TestVehicles.Create(unitNumber: "U-04");
        var addressed = Daily(vehicle.Id, 4, "Mirror");
        var linked = Daily(vehicle.Id, 1, "Mirror");
        var legacy = Daily(vehicleId: null, 2, "mirror", unit: "U-04");
        var legacyOtherUnit = Daily(vehicleId: null, 3, "Mirror", unit: "U-07");
        var repository = new InMemoryVehicleInspectionRepository();
        var vehicles = new InMemoryVehicleRepository();
        vehicles.Add(vehicle);
        foreach (var i in new[] { addressed, linked, legacy, legacyOtherUnit })
        {
            repository.Add(i);
        }

        var cascade = TestDuplicateCascade.Over(repository, vehicles);
        var preview = new GetDefectDuplicatesQueryHandler(repository, cascade);
        var resolve = new ResolveInspectionDefectCommandHandler(repository, cascade);

        var shown = (await preview.Handle(Query(addressed.Id, "Mirror"), CancellationToken.None)).Value.Duplicates;

        var result = await resolve.Handle(
            new ResolveInspectionDefectCommand(
                TestVehicles.TenantId, addressed.Id, "Mirror", DefectResolutionReason.PreviouslyRepaired,
                Note: null, ResolvedBy: "Dispatch",
                ClearDuplicates: [.. shown.Select(d => new DuplicateDefectReference(d.InspectionId, d.Item))]),
            CancellationToken.None);

        Assert.Equal([linked.Id, legacy.Id], shown.Select(d => d.InspectionId));
        Assert.Equal(
            shown.Select(d => (d.InspectionId, d.Item)),
            result.Value.Resolved.Skip(1).Select(r => (r.InspectionId, r.Item)));
        Assert.False(legacyOtherUnit.Defects.Single().IsResolved);
    }
}

using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Application.Inspections.Update;
using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The amend handler: loads tenant-filtered (a missing/cross-tenant id yields
/// <see cref="InspectionErrors.NotFound"/>), then delegates to <see cref="VehicleInspection.Amend"/>
/// and saves.
/// </summary>
public class UpdateInspectionCommandHandlerTests
{
    private static UpdateInspectionCommand Command(Guid inspectionId, IReadOnlyList<DefectInput> defects) =>
        new(
            inspectionId,
            InspectionSource.Dispatcher,
            VehicleId: null,
            Unit: "U-04",
            DriverName: "J. Spence",
            EnteredBy: null,
            PerformedAt: DateTimeOffset.UtcNow,
            OdometerKm: 118_500,
            Checklist: [],
            Defects: defects,
            Weather: [],
            TemperatureC: null,
            RoadConditions: [],
            Visibility: null,
            RoadAdvisories: null,
            FuelLevel: null,
            Issues: [],
            Attestations: [],
            DriverSignatureName: null,
            CertifiedAt: null,
            FuelAdded: false,
            FuelLitres: null,
            FuelCostCad: null);

    [Fact]
    public async Task An_unknown_id_returns_not_found()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new UpdateInspectionCommandHandler(repository);

        var result = await handler.Handle(Command(Guid.NewGuid(), []), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.NotFound, result.Error);
        Assert.Equal(0, repository.SaveChangesCallCount); // nothing to save
    }

    [Fact]
    public async Task Amends_the_stored_inspection_re_derives_the_result_and_saves()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var stored = TestInspections.PreTrip(defects: []); // Pass to begin with
        repository.Add(stored);
        Assert.Equal(InspectionResult.Pass, stored.Result);

        var handler = new UpdateInspectionCommandHandler(repository);
        var result = await handler.Handle(
            Command(stored.Id, [new DefectInput("Brakes", InspectionDefectSeverity.OutOfService, null)]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InspectionResult.Fail, stored.Result);
        // Identity is untouched by the amend.
        Assert.Equal(InspectionType.PreTrip, stored.Type);
        Assert.Equal("TR-4818", stored.TripNumber);
        Assert.Equal(118_500, stored.OdometerKm);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }
}

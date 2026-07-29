using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The one-pre-trip-and-one-post-trip-per-trip uniqueness guard on the enter path. Only
/// trip-context inspections are guarded (a trip number is present); standalone vehicle entries
/// with no trip number can repeat. The check is keyed on (tenant, trip number, type).
/// </summary>
public class EnterInspectionUniquenessTests
{
    private static EnterInspectionCommand Command(InspectionType type, string? tripNumber) =>
        new(
            TestVehicles.TenantId,
            InspectionSource.Dispatcher,
            type,
            TripNumber: tripNumber,
            VehicleId: null,
            Unit: "U-04",
            DriverName: "J. Spence",
            EnteredBy: null,
            PerformedAt: DateTimeOffset.UtcNow,
            OdometerKm: 118_204,
            Checklist: [],
            Defects: [],
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
    public async Task A_second_pre_trip_for_the_same_trip_is_rejected()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new EnterInspectionCommandHandler(repository);

        var first = await handler.Handle(Command(InspectionType.PreTrip, "TR-4818"), CancellationToken.None);
        var second = await handler.Handle(Command(InspectionType.PreTrip, "TR-4818"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal(InspectionErrors.DuplicateForTrip(InspectionType.PreTrip), second.Error);
        Assert.Single(repository.Inspections); // the second was never added
    }

    [Fact]
    public async Task A_second_post_trip_for_the_same_trip_is_rejected()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new EnterInspectionCommandHandler(repository);

        var first = await handler.Handle(Command(InspectionType.PostTrip, "TR-4818"), CancellationToken.None);
        var second = await handler.Handle(Command(InspectionType.PostTrip, "TR-4818"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal(InspectionErrors.DuplicateForTrip(InspectionType.PostTrip), second.Error);
        Assert.Single(repository.Inspections);
    }

    [Fact]
    public async Task A_pre_trip_and_a_post_trip_for_the_same_trip_both_succeed()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new EnterInspectionCommandHandler(repository);

        var pre = await handler.Handle(Command(InspectionType.PreTrip, "TR-4818"), CancellationToken.None);
        var post = await handler.Handle(Command(InspectionType.PostTrip, "TR-4818"), CancellationToken.None);

        Assert.True(pre.IsSuccess);
        Assert.True(post.IsSuccess);
        Assert.Equal(2, repository.Inspections.Count);
    }

    [Fact]
    public async Task Two_pre_trips_for_different_trips_both_succeed()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new EnterInspectionCommandHandler(repository);

        var first = await handler.Handle(Command(InspectionType.PreTrip, "TR-4818"), CancellationToken.None);
        var second = await handler.Handle(Command(InspectionType.PreTrip, "TR-4900"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, repository.Inspections.Count);
    }

    [Fact]
    public async Task Standalone_entries_without_a_trip_number_are_not_guarded()
    {
        // The guard only applies to trip-context inspections; two ad-hoc pre-trips with no trip
        // number (e.g. a bare vehicle check) are allowed to repeat.
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new EnterInspectionCommandHandler(repository);

        var first = await handler.Handle(Command(InspectionType.PreTrip, tripNumber: null), CancellationToken.None);
        var second = await handler.Handle(Command(InspectionType.PreTrip, tripNumber: null), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, repository.Inspections.Count);
    }
}

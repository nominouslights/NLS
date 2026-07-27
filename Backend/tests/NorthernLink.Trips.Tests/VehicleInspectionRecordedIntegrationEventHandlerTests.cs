using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class VehicleInspectionRecordedIntegrationEventHandlerTests
{
    private readonly FakeTripRepository _trips = new();

    private VehicleInspectionRecordedIntegrationEventHandler Handler =>
        new(_trips, NullLogger<VehicleInspectionRecordedIntegrationEventHandler>.Instance);

    private static VehicleInspectionRecordedIntegrationEvent Event(
        string inspectionType, string? tripNumber) =>
        new(
            InspectionId: Guid.NewGuid(),
            TenantId: TestPlanning.TenantId,
            TripNumber: tripNumber,
            InspectionType: inspectionType,
            VehicleId: Guid.NewGuid(),
            Source: "Dispatcher");

    [Fact]
    public async Task Post_trip_inspection_sets_the_flag_on_the_matching_trip()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-4818").Value;
        _trips.Add(trip);

        await Handler.Handle(Event("PostTrip", "TR-4818"), CancellationToken.None);

        Assert.True(trip.HasPostTripInspection);
        Assert.Equal(1, _trips.SaveCount);
        Assert.True(trip.Complete().IsSuccess); // gate is now cleared
    }

    [Fact]
    public async Task Redelivery_converges_idempotently()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-4818").Value;
        _trips.Add(trip);
        var evt = Event("PostTrip", "TR-4818");

        await Handler.Handle(evt, CancellationToken.None);
        await Handler.Handle(evt, CancellationToken.None);

        Assert.True(trip.HasPostTripInspection);
    }

    [Fact]
    public async Task Pre_trip_inspection_is_ignored()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-4818").Value;
        _trips.Add(trip);

        await Handler.Handle(Event("PreTrip", "TR-4818"), CancellationToken.None);

        Assert.False(trip.HasPostTripInspection);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Unknown_trip_number_is_a_success_no_op()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-4818").Value;
        _trips.Add(trip);

        await Handler.Handle(Event("PostTrip", "TR-9999"), CancellationToken.None);

        Assert.False(trip.HasPostTripInspection);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Missing_trip_number_is_a_success_no_op()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-4818").Value;
        _trips.Add(trip);

        await Handler.Handle(Event("PostTrip", null), CancellationToken.None);

        Assert.False(trip.HasPostTripInspection);
        Assert.Equal(0, _trips.SaveCount);
    }
}

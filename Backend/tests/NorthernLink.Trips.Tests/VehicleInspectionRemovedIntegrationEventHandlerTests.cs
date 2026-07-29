using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The inverse of the recorded handler: a Fleet <c>PostTrip</c> removal carrying a trip number
/// clears the matching trip's post-trip-inspection flag, re-arming <c>Trip.Complete()</c>.
/// Pre-trip removals, removals with no trip context, and removals for an unknown trip are ignored.
/// </summary>
public class VehicleInspectionRemovedIntegrationEventHandlerTests
{
    private readonly FakeTripRepository _trips = new();

    private VehicleInspectionRemovedIntegrationEventHandler Handler =>
        new(_trips, NullLogger<VehicleInspectionRemovedIntegrationEventHandler>.Instance);

    private static VehicleInspectionRemovedIntegrationEvent Event(string inspectionType, string? tripNumber) =>
        new(
            InspectionId: Guid.NewGuid(),
            TenantId: TestPlanning.TenantId,
            TripNumber: tripNumber,
            InspectionType: inspectionType);

    private Trip TripWithPostTripInspection(string tripNumber = "TR-4818")
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: tripNumber).Value;
        trip.RecordPostTripInspection();
        _trips.Add(trip);
        return trip;
    }

    [Fact]
    public async Task Post_trip_removal_clears_the_flag_and_re_gates_completion()
    {
        var trip = TripWithPostTripInspection();
        Assert.True(trip.HasPostTripInspection);

        await Handler.Handle(Event("PostTrip", "TR-4818"), CancellationToken.None);

        Assert.False(trip.HasPostTripInspection);
        Assert.Equal(1, _trips.SaveCount);

        // Gate re-armed — completion is refused again.
        var complete = trip.Complete();
        Assert.True(complete.IsFailure);
        Assert.Equal(TripErrors.PostTripInspectionRequired, complete.Error);
    }

    [Fact]
    public async Task A_pre_trip_removal_is_ignored()
    {
        var trip = TripWithPostTripInspection();

        await Handler.Handle(Event("PreTrip", "TR-4818"), CancellationToken.None);

        Assert.True(trip.HasPostTripInspection); // untouched
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task A_removal_with_no_trip_number_is_a_success_no_op()
    {
        var trip = TripWithPostTripInspection();

        await Handler.Handle(Event("PostTrip", null), CancellationToken.None);

        Assert.True(trip.HasPostTripInspection);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task An_unknown_trip_number_is_a_success_no_op()
    {
        var trip = TripWithPostTripInspection();

        await Handler.Handle(Event("PostTrip", "TR-9999"), CancellationToken.None);

        Assert.True(trip.HasPostTripInspection);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Redelivery_converges_idempotently()
    {
        var trip = TripWithPostTripInspection();
        var evt = Event("PostTrip", "TR-4818");

        await Handler.Handle(evt, CancellationToken.None);
        await Handler.Handle(evt, CancellationToken.None);

        Assert.False(trip.HasPostTripInspection); // still cleared; no flip-flop
    }
}

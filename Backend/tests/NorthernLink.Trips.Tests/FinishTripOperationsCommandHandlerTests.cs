using NorthernLink.Trips.Application.Trips.FinishOperations;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class FinishTripOperationsCommandHandlerTests
{
    private readonly FakeTripRepository _trips = new();

    private FinishTripOperationsCommandHandler Handler => new(_trips);

    [Fact]
    public async Task Finishing_a_client_trip_lands_in_ready_for_billing()
    {
        var trip = TestPlanning.ScheduleTrip(clientId: Guid.NewGuid()).Value;
        trip.RecordPostTripInspection();
        _trips.Add(trip);

        var result = await Handler.Handle(new FinishTripOperationsCommand(trip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.ReadyForBilling, trip.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Finishing_a_deadhead_needs_no_inspection()
    {
        // The dispatcher's "bill the deadhead" path: an empty leg has no inspection of its own
        // (Fleet logs one against the trip the vehicle actually worked) and goes straight from
        // Scheduled to ReadyForBilling so the round trip can be invoiced.
        var outbound = TestPlanning.ScheduleTrip(clientId: Guid.NewGuid()).Value;
        var deadhead = outbound.CreateDeadheadReturn("TR-1002").Value;
        _trips.Add(deadhead);

        var result = await Handler.Handle(new FinishTripOperationsCommand(deadhead.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.ReadyForBilling, deadhead.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Finishing_a_clientless_trip_completes_it()
    {
        var trip = TestPlanning.ScheduleTrip().Value; // community/walk-up shape
        trip.RecordPostTripInspection();
        _trips.Add(trip);

        var result = await Handler.Handle(new FinishTripOperationsCommand(trip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Completed, trip.Status);
    }

    [Fact]
    public async Task Finish_still_requires_the_post_trip_inspection()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);

        var result = await Handler.Handle(new FinishTripOperationsCommand(trip.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PostTripInspectionRequired, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Finish_needs_no_passenger_manifest()
    {
        // The manifest guard gates going en route, not coming back — a Scheduled trip whose
        // dispatcher forgot to press START can still be finished directly.
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.RecordPostTripInspection();
        _trips.Add(trip);

        var result = await Handler.Handle(new FinishTripOperationsCommand(trip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Unknown_trip_is_not_found()
    {
        var result = await Handler.Handle(new FinishTripOperationsCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotFound, result.Error);
    }
}

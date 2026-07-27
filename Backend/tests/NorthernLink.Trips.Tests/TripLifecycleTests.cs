using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class TripLifecycleTests
{
    [Theory]
    [InlineData(TripStatus.Scheduled, TripStatus.InProgress, true)]
    [InlineData(TripStatus.Scheduled, TripStatus.Completed, true)]
    [InlineData(TripStatus.Scheduled, TripStatus.Cancelled, true)]
    [InlineData(TripStatus.InProgress, TripStatus.Completed, true)]
    [InlineData(TripStatus.InProgress, TripStatus.Cancelled, true)]
    [InlineData(TripStatus.InProgress, TripStatus.Scheduled, false)]
    [InlineData(TripStatus.Completed, TripStatus.Scheduled, false)]
    [InlineData(TripStatus.Completed, TripStatus.InProgress, false)]
    [InlineData(TripStatus.Completed, TripStatus.Cancelled, false)]
    [InlineData(TripStatus.Cancelled, TripStatus.Scheduled, false)]
    [InlineData(TripStatus.Cancelled, TripStatus.InProgress, false)]
    [InlineData(TripStatus.Cancelled, TripStatus.Completed, false)]
    public void Transition_matrix(TripStatus from, TripStatus to, bool allowed)
    {
        Assert.Equal(allowed, Trip.CanTransition(from, to));
    }

    [Fact]
    public void Schedule_creates_a_scheduled_trip_and_raises_the_scheduled_event()
    {
        var result = TestPlanning.ScheduleTrip();

        Assert.True(result.IsSuccess);
        var trip = result.Value;
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(0, trip.SeatsConfirmed);
        var scheduled = Assert.IsType<TripScheduledDomainEvent>(Assert.Single(trip.DomainEvents));
        Assert.Equal(trip.Id, scheduled.TripId);
    }

    [Fact]
    public void Complete_sets_the_timestamp_and_raises_the_completed_event()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.RecordPostTripInspection();
        trip.ClearDomainEvents();

        var result = trip.Complete();

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.NotNull(trip.CompletedAtUtc);
        var completed = Assert.IsType<TripCompletedDomainEvent>(Assert.Single(trip.DomainEvents));
        Assert.Equal(trip.Id, completed.TripId);
    }

    [Fact]
    public void Complete_is_rejected_without_a_logged_post_trip_inspection()
    {
        var trip = TestPlanning.ScheduleTrip().Value;

        var result = trip.Complete();

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PostTripInspectionRequired, result.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status); // refused via the direct Scheduled -> Completed path
        Assert.Null(trip.CompletedAtUtc);
    }

    [Fact]
    public void Complete_is_allowed_once_a_post_trip_inspection_is_recorded()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        Assert.True(trip.Complete().IsFailure); // gated before

        Assert.True(trip.RecordPostTripInspection().IsSuccess);
        var result = trip.Complete();

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Completed, trip.Status);
    }

    [Fact]
    public void Record_post_trip_inspection_is_idempotent_and_raises_one_event()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.ClearDomainEvents();

        Assert.True(trip.RecordPostTripInspection().IsSuccess);
        Assert.True(trip.HasPostTripInspection);
        Assert.Single(trip.DomainEvents.OfType<TripPostTripInspectionRecordedDomainEvent>());

        var second = trip.RecordPostTripInspection(); // redelivery converges

        Assert.True(second.IsSuccess);
        Assert.True(trip.HasPostTripInspection);
        Assert.Single(trip.DomainEvents.OfType<TripPostTripInspectionRecordedDomainEvent>()); // no second event
    }

    [Fact]
    public void Start_then_complete_walks_the_happy_path()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.RecordPostTripInspection();

        Assert.True(trip.Start().IsSuccess);
        Assert.Equal(TripStatus.InProgress, trip.Status);
        Assert.True(trip.Complete().IsSuccess);
    }

    [Fact]
    public void Start_on_an_in_progress_trip_is_rejected()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.Start();

        var result = trip.Start();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public void Terminal_trips_reject_further_transitions()
    {
        var completed = TestPlanning.ScheduleTrip().Value;
        completed.RecordPostTripInspection();
        completed.Complete();
        Assert.True(completed.Start().IsFailure);
        Assert.True(completed.Complete().IsFailure);
        Assert.True(completed.Cancel(null).IsFailure);

        var cancelled = TestPlanning.ScheduleTrip().Value;
        cancelled.Cancel("Weather");
        Assert.True(cancelled.Start().IsFailure);
        Assert.True(cancelled.Complete().IsFailure);
        Assert.Equal("Weather", cancelled.CancelledReason);
    }

    [Fact]
    public void Update_is_only_allowed_while_scheduled()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.Start();

        var result = trip.Update(
            trip.ServiceDate, trip.WindowStart, trip.WindowEnd, trip.ServiceType,
            trip.RouteId, trip.RouteName, trip.Origin, trip.Destination, trip.Stops,
            trip.DistanceKm, trip.IsEmptyLeg, trip.ClientId, trip.ClientName,
            trip.PoNumber, trip.SeatsCapacity, trip.SeatsMinimum);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotEditable, result.Error);
    }

    [Fact]
    public void Assign_and_unassign_driver_snapshot_the_name()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        var driverId = Guid.NewGuid();

        Assert.True(trip.AssignDriver(driverId, "R. Ballantyne").IsSuccess);
        Assert.Equal(driverId, trip.DriverId);
        Assert.Equal("R. Ballantyne", trip.DriverName);

        Assert.True(trip.UnassignDriver().IsSuccess);
        Assert.Null(trip.DriverId);
        Assert.Null(trip.DriverName);
    }

    [Fact]
    public void Assignment_is_rejected_on_a_terminal_trip()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.RecordPostTripInspection();
        trip.Complete();

        Assert.True(trip.AssignDriver(Guid.NewGuid(), "R. Ballantyne").IsFailure);
        Assert.True(trip.UnassignDriver().IsFailure);
        Assert.True(trip.AssignVehicle(Guid.NewGuid(), "U-07").IsFailure);
    }

    [Fact]
    public void Demand_is_recorded_within_capacity()
    {
        var trip = TestPlanning.ScheduleTrip(seatsCapacity: 12).Value;

        Assert.True(trip.RecordDemand(9, demandGuaranteed: true).IsSuccess);
        Assert.Equal(9, trip.SeatsConfirmed);
        Assert.True(trip.DemandGuaranteed);

        var overCapacity = trip.RecordDemand(13, demandGuaranteed: false);
        Assert.Equal(TripErrors.SeatsExceedCapacity, overCapacity.Error);

        var negative = trip.RecordDemand(-1, demandGuaranteed: false);
        Assert.Equal(TripErrors.InvalidSeats, negative.Error);
    }

    [Fact]
    public void Attach_manifest_links_without_changing_status()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.ClearDomainEvents();
        var manifestId = Guid.NewGuid();

        var result = trip.AttachManifest(manifestId);

        Assert.True(result.IsSuccess);
        Assert.Equal(manifestId, trip.ManifestId);
        Assert.Equal(TripStatus.Scheduled, trip.Status); // linking never completes
        Assert.Empty(trip.DomainEvents.OfType<TripCompletedDomainEvent>());
        Assert.Single(trip.DomainEvents.OfType<TripManifestLinkedDomainEvent>());
    }

    [Fact]
    public void Attach_manifest_is_idempotent_for_the_same_manifest()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        var manifestId = Guid.NewGuid();
        trip.AttachManifest(manifestId);
        trip.ClearDomainEvents();

        var result = trip.AttachManifest(manifestId);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Empty(trip.DomainEvents); // no second link event
    }

    [Fact]
    public void Attach_manifest_rejects_a_second_different_manifest()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.AttachManifest(Guid.NewGuid());

        var result = trip.AttachManifest(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.ManifestAlreadyAttached, result.Error);
    }
}

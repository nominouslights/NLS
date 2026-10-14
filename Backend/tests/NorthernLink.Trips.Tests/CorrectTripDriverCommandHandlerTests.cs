using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Application.Trips.CorrectDriver;
using NorthernLink.Trips.Application.Trips.GetActivity;
using NorthernLink.Trips.Application.Trips.GetTripById;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class CorrectTripDriverCommandHandlerTests
{
    private static readonly Guid OldDriverUserId = Guid.Parse("00000000-0000-0000-0000-00000000ab01");
    private static readonly Guid NewDriverUserId = Guid.Parse("00000000-0000-0000-0000-00000000ab02");
    private static readonly Guid NewDriverId = Guid.Parse("00000000-0000-0000-0000-0000000000d2");
    private static readonly Guid ActorUserId = Guid.Parse("00000000-0000-0000-0000-00000000aa09");

    private readonly FakeTripRepository _trips = new();
    private readonly FakeDriverLookupRepository _drivers = new();
    private readonly FakeCurrentActor _actor = new(ActorUserId, Roles.Dispatcher) { Email = "dispatch@northernlink.ca" };

    public CorrectTripDriverCommandHandlerTests()
    {
        _drivers.Drivers.Add(TestPlanning.ActiveDriver(userId: OldDriverUserId));
        _drivers.Drivers.Add(NewDriver(DriverLookup.ActiveStatus));
    }

    private CorrectTripDriverCommandHandler Handler => new(_trips, _drivers, _actor);

    private static DriverLookup NewDriver(string status) => new()
    {
        DriverId = NewDriverId,
        TenantId = TestPlanning.TenantId,
        Name = "J. Moose",
        LicenceClass = "Class 4",
        Status = status,
        UserId = NewDriverUserId,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    private Trip SeedFinishedTrip(TripStatus status = TripStatus.ReadyForBilling)
    {
        var trip = TripDriverCorrectionTests.TripIn(status);
        _trips.Add(trip);
        return trip;
    }

    [Fact]
    public async Task Unknown_trip_is_NotFound()
    {
        var result = await Handler.Handle(
            new CorrectTripDriverCommand(Guid.NewGuid(), NewDriverId, "wrong driver"), CancellationToken.None);

        Assert.Equal(TripErrors.NotFound, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Unknown_driver_is_DriverNotFound()
    {
        var trip = SeedFinishedTrip();

        var result = await Handler.Handle(
            new CorrectTripDriverCommand(trip.Id, Guid.NewGuid(), "wrong driver"), CancellationToken.None);

        Assert.Equal(TripErrors.DriverNotFound, result.Error);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal(TestPlanning.DriverId, trip.DriverId);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Theory]
    [InlineData("Inactive")]
    [InlineData("Deactivated")]
    public async Task An_inactive_or_deactivated_driver_is_accepted_because_it_is_history(string status)
    {
        _drivers.Drivers.RemoveAll(d => d.DriverId == NewDriverId);
        _drivers.Drivers.Add(NewDriver(status));
        var trip = SeedFinishedTrip(TripStatus.Completed);

        var result = await Handler.Handle(
            new CorrectTripDriverCommand(trip.Id, NewDriverId, "wrong driver"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewDriverId, trip.DriverId);
        Assert.Equal("J. Moose", trip.DriverName);
    }

    [Fact]
    public async Task Who_comes_from_the_current_actor_and_the_trip_is_saved_once()
    {
        var trip = SeedFinishedTrip(TripStatus.Invoiced);

        var result = await Handler.Handle(
            new CorrectTripDriverCommand(trip.Id, NewDriverId, "  wrong driver  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _trips.SaveCount);
        var raised = Assert.IsType<TripDriverCorrectedDomainEvent>(Assert.Single(trip.DomainEvents));
        Assert.Equal(ActorUserId, raised.EnteredByUserId);
        Assert.Equal("dispatch@northernlink.ca", raised.EnteredBy);
        Assert.Equal("wrong driver", raised.Reason);
        Assert.Equal("J. Moose", raised.NewDriverName); // the lookup's name, not anything the caller sent
    }

    [Fact]
    public async Task No_principal_records_no_actor_rather_than_failing()
    {
        var trip = SeedFinishedTrip();

        var result = await new CorrectTripDriverCommandHandler(_trips, _drivers, new FakeCurrentActor(null)).Handle(
            new CorrectTripDriverCommand(trip.Id, NewDriverId, "wrong driver"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var raised = Assert.IsType<TripDriverCorrectedDomainEvent>(Assert.Single(trip.DomainEvents));
        Assert.Null(raised.EnteredByUserId);
        Assert.Null(raised.EnteredBy);
    }

    [Fact]
    public async Task A_missing_driver_id_is_the_domains_DriverRequired_validation_not_DriverNotFound()
    {
        // The endpoint maps an absent/empty driverId to Guid.Empty; that is a malformed request
        // (400), not an unknown driver (404).
        var trip = SeedFinishedTrip();

        var result = await Handler.Handle(
            new CorrectTripDriverCommand(trip.Id, Guid.Empty, "wrong driver"), CancellationToken.None);

        Assert.Equal(TripErrors.DriverRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TestPlanning.DriverId, trip.DriverId);
        Assert.Empty(trip.DomainEvents);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task A_missing_driver_id_on_a_live_trip_still_reports_the_wrong_path_first()
    {
        var trip = TripDriverCorrectionTests.TripIn(TripStatus.InProgress);
        _trips.Add(trip);

        var result = await Handler.Handle(
            new CorrectTripDriverCommand(trip.Id, Guid.Empty, ""), CancellationToken.None);

        Assert.Equal("Trips.Trip.UseReassign", result.Error.Code);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Domain_refusals_pass_through_without_saving()
    {
        var trip = TripDriverCorrectionTests.TripIn(TripStatus.Scheduled);
        _trips.Add(trip);

        var result = await Handler.Handle(
            new CorrectTripDriverCommand(trip.Id, NewDriverId, "wrong driver"), CancellationToken.None);

        Assert.Equal("Trips.Trip.UseReassign", result.Error.Code);
        Assert.Equal(0, _trips.SaveCount);
    }

    // ---- The identity gate reads trip.DriverId live, so access follows the correction ----

    [Fact]
    public async Task After_a_correction_the_new_driver_reads_the_activity_and_the_old_one_is_NotYourTrip()
    {
        var trip = SeedFinishedTrip();
        Assert.True((await Handler.Handle(
            new CorrectTripDriverCommand(trip.Id, NewDriverId, "wrong driver"), CancellationToken.None)).IsSuccess);

        var activity = new FakeTripActivityReadService();
        activity.Entries.Add(new TripActivityJournalEntry(DateTimeOffset.UtcNow, "trip", "trip-driver-corrected",
            """{"previousDriverName":"R. Ballantyne","newDriverName":"J. Moose","reason":"wrong driver","enteredBy":"dispatch@northernlink.ca"}"""));
        GetTripActivityQueryHandler ActivityAs(Guid userId) =>
            new(_trips, activity, new TripOperatorAccess(FakeCurrentActor.Driver(userId), _drivers));

        var asNew = await ActivityAs(NewDriverUserId).Handle(
            new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None);
        var asOld = await ActivityAs(OldDriverUserId).Handle(
            new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None);
        var asDispatch = await new GetTripActivityQueryHandler(
                _trips, activity, new TripOperatorAccess(FakeCurrentActor.Dispatcher, _drivers))
            .Handle(new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None);

        Assert.True(asNew.IsSuccess);
        Assert.Equal(TripErrors.NotYourTrip, asOld.Error);

        // The new driver sees that the driver was corrected, but not the dispatch-side narrative.
        var driverView = Assert.Single(asNew.Value);
        Assert.Equal("trip-driver-corrected", driverView.EventType);
        Assert.Null(driverView.Reason);
        Assert.Null(driverView.Detail);
        Assert.Null(driverView.EnteredBy);

        var dispatchView = Assert.Single(asDispatch.Value);
        Assert.Equal("wrong driver", dispatchView.Reason);
        Assert.Equal("Driver: R. Ballantyne → J. Moose", dispatchView.Detail);
        Assert.Equal("dispatch@northernlink.ca", dispatchView.EnteredBy);
    }

    [Fact]
    public async Task After_a_correction_the_new_driver_reads_the_trip_and_the_old_one_is_NotYourTrip()
    {
        // GET /api/trips/{id} reads the projected row; this models the row after the projection
        // has caught up with the corrected DriverId.
        var reads = new FakeTripReadService();
        var tripId = Guid.NewGuid();
        reads.Trips.Add(new TripResponse(
            tripId, "TR-1001", new DateOnly(2026, 7, 21), new TimeOnly(6, 30), new TimeOnly(8, 15), "ContractCrew",
            null, "Thompson ↔ Lynn Lake", "Thompson", "MB", "Lynn Lake", "MB", [], 320, null, null, null, false,
            null, "Alamos Gold", "PO-2026-118", NewDriverId, "J. Moose", TestPlanning.VehicleId,
            TestPlanning.VehicleUnit, 12, 0, null, false, "ReadyForBilling", null, true, null, null, null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));

        GetTripByIdQueryHandler As(Guid userId) =>
            new(reads, new TripOperatorAccess(FakeCurrentActor.Driver(userId), _drivers));

        var asNew = await As(NewDriverUserId).Handle(new GetTripByIdQuery(TestPlanning.TenantId, tripId), CancellationToken.None);
        var asOld = await As(OldDriverUserId).Handle(new GetTripByIdQuery(TestPlanning.TenantId, tripId), CancellationToken.None);

        Assert.True(asNew.IsSuccess);
        Assert.Equal(TripErrors.NotYourTrip, asOld.Error);
    }
}

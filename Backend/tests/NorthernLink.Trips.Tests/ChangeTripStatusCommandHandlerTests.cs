using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Application.Trips.ChangeStatus;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class ChangeTripStatusCommandHandlerTests
{
    private static readonly Guid DriverAUserId = Guid.Parse("00000000-0000-0000-0000-00000000ab01");
    private static readonly Guid DriverBUserId = Guid.Parse("00000000-0000-0000-0000-00000000ab02");
    private static readonly Guid DriverBId = Guid.Parse("00000000-0000-0000-0000-0000000000d2");

    private readonly FakeTripRepository _trips = new();
    private readonly FakeTripManifestRepository _manifests = new();
    private readonly FakeShipmentRepository _shipments = new();
    private readonly FakeDriverLookupRepository _drivers = new();

    /// <summary>Dispatch by default, so the status-rule tests below are untouched by the identity gate.</summary>
    private ICurrentActor _actor = FakeCurrentActor.Dispatcher;

    private ChangeTripStatusCommandHandler Handler =>
        new(_trips, _manifests, _shipments, new TripOperatorAccess(_actor, _drivers));

    // ---- The identity gate: only the trip's own driver (or dispatch) may advance it ----

    /// <summary>Two linked drivers on the replica: A owns the baseline driver row, B another.</summary>
    private void LinkTwoDrivers()
    {
        _drivers.Drivers.Add(TestPlanning.ActiveDriver(userId: DriverAUserId));
        _drivers.Drivers.Add(TestPlanning.ActiveDriver(userId: DriverBUserId, driverId: DriverBId));
    }

    [Fact]
    public async Task A_driver_cannot_change_another_drivers_trip()
    {
        // The hole this closes: DriverAccess admits every driver, and the route carries no
        // {driverId}, so without the gate driver A could cancel driver B's run from the cab.
        LinkTwoDrivers();
        var theirs = TestPlanning.ScheduleTrip(driverId: DriverBId, isEmptyLeg: true).Value;
        _trips.Add(theirs);
        _actor = FakeCurrentActor.Driver(DriverAUserId);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(theirs.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotYourTrip, result.Error);
        Assert.Equal("Trips.Trip.NotYourTrip", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal(TripStatus.Scheduled, theirs.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task The_assigned_driver_can_change_their_own_trip()
    {
        LinkTwoDrivers();
        var mine = TestPlanning.ScheduleTrip(isEmptyLeg: true).Value; // baseline driver = A's row
        _trips.Add(mine);
        _actor = FakeCurrentActor.Driver(DriverAUserId);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(mine.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.InProgress, mine.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Dispatcher)]
    [InlineData(Roles.Supervisor)]
    public async Task Dispatch_roles_can_change_any_drivers_trip(string role)
    {
        LinkTwoDrivers();
        var theirs = TestPlanning.ScheduleTrip(driverId: DriverBId, isEmptyLeg: true).Value;
        _trips.Add(theirs);
        _actor = new FakeCurrentActor(Guid.NewGuid(), role);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(theirs.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.InProgress, theirs.Status);
        // The override short-circuits before the lookup: a dispatcher's request never pays for it.
        Assert.Equal(0, _drivers.UserLookups);
    }

    [Fact]
    public async Task A_driver_account_linked_to_no_driver_is_refused()
    {
        // An account with the Driver role but no driver_lookup row pointing at it (never linked,
        // or unlinked — the unlink event writes null through) owns no trips at all.
        LinkTwoDrivers();
        var trip = TestPlanning.ScheduleTrip(isEmptyLeg: true).Value;
        _trips.Add(trip);
        _actor = FakeCurrentActor.Driver(Guid.NewGuid());

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotYourTrip, result.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
    }

    [Fact]
    public async Task A_caller_with_no_principal_is_refused()
    {
        // No actor must never read as "owns everything".
        LinkTwoDrivers();
        var trip = TestPlanning.ScheduleTrip(isEmptyLeg: true).Value;
        _trips.Add(trip);
        _actor = new FakeCurrentActor(null, Roles.Driver);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotYourTrip, result.Error);
    }

    [Fact]
    public async Task A_driver_cannot_cancel_another_drivers_trip_either()
    {
        // The gate sits in front of every status, not just Start.
        LinkTwoDrivers();
        var theirs = TestPlanning.ScheduleTrip(driverId: DriverBId).Value;
        _trips.Add(theirs);
        _actor = FakeCurrentActor.Driver(DriverAUserId);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(theirs.Id, TripStatus.Cancelled, "Not mine"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotYourTrip, result.Error);
        Assert.Equal(TripStatus.Scheduled, theirs.Status);
    }

    [Fact]
    public async Task The_gate_runs_before_the_en_route_rules_so_a_stranger_learns_nothing()
    {
        // Driver A against B's passenger trip with no manifest: the answer is NotYourTrip, not
        // PassengerManifestRequired — the rule failures describe the trip, and a stranger gets
        // none of that.
        LinkTwoDrivers();
        var theirs = TestPlanning.ScheduleTrip(driverId: DriverBId).Value;
        _trips.Add(theirs);
        _actor = FakeCurrentActor.Driver(DriverAUserId);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(theirs.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.Equal(TripErrors.NotYourTrip, result.Error);
    }

    [Fact]
    public async Task Role_matching_for_the_dispatch_override_is_case_sensitive()
    {
        // "dispatcher" is a role User.Create refuses to store; it must not buy the override here.
        LinkTwoDrivers();
        var theirs = TestPlanning.ScheduleTrip(driverId: DriverBId, isEmptyLeg: true).Value;
        _trips.Add(theirs);
        _actor = new FakeCurrentActor(Guid.NewGuid(), "dispatcher");

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(theirs.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.Equal(TripErrors.NotYourTrip, result.Error);
    }

    // ---- Status rules (dispatch caller) ----

    [Fact]
    public async Task Start_is_rejected_when_the_trip_has_no_manifest()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PassengerManifestRequired, result.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Start_is_rejected_when_the_manifest_has_zero_passengers()
    {
        var manifest = TestManifests.Create(passengers: []).Value;
        _manifests.Add(manifest);
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.AttachManifest(manifest.Id);
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PassengerManifestRequired, result.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
    }

    [Fact]
    public async Task Start_is_allowed_with_a_manifest_carrying_at_least_one_passenger()
    {
        var manifest = TestManifests.Create().Value; // one passenger
        _manifests.Add(manifest);
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.AttachManifest(manifest.Id);
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.InProgress, trip.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Start_of_a_deadhead_needs_no_manifest()
    {
        // An empty repositioning leg starts and ends without passengers — the en-route
        // manifest guard is waived for IsEmptyLeg trips.
        var deadhead = TestPlanning.ScheduleTrip(isEmptyLeg: true).Value;
        _trips.Add(deadhead);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(deadhead.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.InProgress, deadhead.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Cargo_start_is_rejected_with_no_shipment_assigned()
    {
        // The relaxed en-route gate: a cargo run needs freight booked on it, not a
        // passenger manifest.
        var trip = TestPlanning.ScheduleTrip(serviceType: TripServiceType.Cargo).Value;
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.ShipmentRequired, result.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Cargo_start_is_allowed_with_at_least_one_shipment_leg_and_no_manifest()
    {
        var trip = TestPlanning.ScheduleTrip(serviceType: TripServiceType.Grocery).Value;
        _trips.Add(trip);
        _shipments.Add(TestShipments.Register().OnTrip(trip.Id, trip.TripNumber, trip.ServiceDate));

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.InProgress, trip.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Cargo_start_is_rejected_when_its_only_shipment_is_cancelled()
    {
        // A dead shipment's stale leg row must not satisfy the gate — the freight is not
        // riding, so the run would leave empty.
        var trip = TestPlanning.ScheduleTrip(serviceType: TripServiceType.Cargo).Value;
        _trips.Add(trip);
        var dead = TestShipments.Register().OnTrip(trip.Id, trip.TripNumber, trip.ServiceDate);
        dead.Cancel("Client pulled the freight");
        _shipments.Add(dead);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.ShipmentRequired, result.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Cargo_start_is_allowed_when_one_live_shipment_rides_beside_a_cancelled_one()
    {
        var trip = TestPlanning.ScheduleTrip(serviceType: TripServiceType.Cargo).Value;
        _trips.Add(trip);
        var dead = TestShipments.Register().OnTrip(trip.Id, trip.TripNumber, trip.ServiceDate);
        dead.Cancel("Client pulled the freight");
        _shipments.Add(dead);
        _shipments.Add(TestShipments.Register(shipmentNumber: "SH-1002")
            .OnTrip(trip.Id, trip.TripNumber, trip.ServiceDate));

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.InProgress, trip.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Cargo_deadhead_needs_neither_shipment_nor_manifest()
    {
        // IsEmptyLeg stays exempt from every en-route gate, cargo included.
        var deadhead = TestPlanning.ScheduleTrip(
            serviceType: TripServiceType.Cargo, isEmptyLeg: true).Value;
        _trips.Add(deadhead);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(deadhead.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.InProgress, deadhead.Status);
    }

    [Fact]
    public async Task Passenger_start_still_demands_a_manifest_even_with_shipments_aboard()
    {
        // Freight riding a passenger run never substitutes for the passenger manifest gate.
        var trip = TestPlanning.ScheduleTrip().Value; // ContractCrew
        _trips.Add(trip);
        _shipments.Add(TestShipments.Register().OnTrip(trip.Id, trip.TripNumber, trip.ServiceDate));

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PassengerManifestRequired, result.Error);
    }

    [Fact]
    public async Task Finish_states_are_refused_here_and_pointed_at_the_finish_command()
    {
        // "Set status to Completed/ReadyForBilling" is a contract the caller can't honour —
        // which of the two a finish lands in depends on the trip's client, so finishing has
        // its own command and this endpoint refuses both names outright.
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.RecordPostTripInspection();
        _trips.Add(trip);

        foreach (var status in new[] { TripStatus.Completed, TripStatus.ReadyForBilling })
        {
            var result = await Handler.Handle(
                new ChangeTripStatusCommand(trip.Id, status, null), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TripErrors.FinishIsItsOwnCommand, result.Error);
        }

        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task System_driven_statuses_can_never_be_set_by_hand()
    {
        var trip = TestPlanning.ScheduleTrip(clientId: Guid.NewGuid()).Value;
        _trips.Add(trip);

        foreach (var status in new[] { TripStatus.Invoiced, TripStatus.WrittenOff })
        {
            var result = await Handler.Handle(
                new ChangeTripStatusCommand(trip.Id, status, null), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TripErrors.SystemDrivenStatus(status), result.Error);
        }

        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Every_status_the_command_accepts_is_deliberate()
    {
        // The handler's switch spells out every member, but C# still forces a discard arm for
        // out-of-range casts — so a NEW enum member would land there silently. This walk fails
        // the moment someone adds a member without deciding what this command does with it.
        var handled = new[]
        {
            TripStatus.Scheduled, TripStatus.InProgress, TripStatus.ReadyForBilling,
            TripStatus.Invoiced, TripStatus.Completed, TripStatus.Cancelled, TripStatus.WrittenOff,
        };

        Assert.Equal(handled.OrderBy(s => s), Enum.GetValues<TripStatus>().OrderBy(s => s));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Cancel_does_not_require_a_passenger_manifest()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.Cancelled, "Weather"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Cancelled, trip.Status);
    }
}

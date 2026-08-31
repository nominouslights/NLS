using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Trips.CreateDeadheadReturn;
using NorthernLink.Trips.Application.Trips.MergeRoundTrip;
using NorthernLink.Trips.Application.Trips.UnpairRoundTrip;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Application-layer wiring for the round-trip commands — validation lives in the
/// aggregate (<see cref="TripRoundTripTests"/>); these cover lookup, number minting,
/// both-legs unpair, and the save.
/// </summary>
public class RoundTripCommandHandlerTests
{
    private static readonly Guid ClientId = Guid.NewGuid();

    private readonly FakeTripRepository _trips = new();
    private readonly FakeTripManifestRepository _manifests = new();

    private MergeRoundTripCommandHandler MergeHandler() =>
        new(_trips, _manifests, new StubCurrentActor(),
            NullLogger<MergeRoundTripCommandHandler>.Instance);

    /// <summary>Stands in for the gateway's JWT-backed actor — only the audit line reads it.</summary>
    private sealed class StubCurrentActor : ICurrentActor
    {
        public Guid? UserId => Guid.Parse("00000000-0000-0000-0000-0000000000a1");

        public string? Email => "dispatcher@northernlink.test";
    }

    /// <summary>Creates a manifest declaring <paramref name="direction"/> and links it to the trip.</summary>
    private void AttachManifest(Trip trip, TripDirection? direction)
    {
        var manifest = TestManifests.Create(tripNumber: trip.TripNumber, direction: direction).Value;
        _manifests.Add(manifest);
        Assert.True(trip.AttachManifest(manifest.Id).IsSuccess);
    }

    private (Trip Outbound, Trip Inbound) AddMergeablePair()
    {
        var outbound = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2001", clientId: ClientId, windowStart: new TimeOnly(6, 30)).Value;
        var inbound = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002", clientId: ClientId, windowStart: new TimeOnly(16, 0),
            origin: "Lynn Lake", destination: "Thompson").Value;
        _trips.Add(outbound);
        _trips.Add(inbound);
        return (outbound, inbound);
    }

    [Fact]
    public async Task Merge_pairs_both_trips_and_saves_once()
    {
        var (outbound, inbound) = AddMergeablePair();
        var handler = MergeHandler();

        var result = await handler.Handle(
            new MergeRoundTripCommand(outbound.Id, inbound.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(outbound.RoundTripKey);
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
        Assert.Equal(TripDirection.Outbound, outbound.Direction);
        Assert.Equal(TripDirection.Inbound, inbound.Direction);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Merge_passes_the_allow_mismatch_override_through_to_the_aggregate()
    {
        // A cross-date pair the strict matcher refuses; the override command pairs it.
        var outbound = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2001", clientId: ClientId, windowStart: new TimeOnly(16, 0)).Value;
        var overnightReturn = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002", clientId: ClientId,
            serviceDate: new DateOnly(2026, 7, 22), windowStart: new TimeOnly(6, 30),
            origin: "Lynn Lake", destination: "Thompson").Value;
        _trips.Add(outbound);
        _trips.Add(overnightReturn);
        var handler = MergeHandler();

        var strict = await handler.Handle(
            new MergeRoundTripCommand(outbound.Id, overnightReturn.Id), CancellationToken.None);
        Assert.Equal(TripErrors.RoundTripServiceDateMismatch, strict.Error); // AllowMismatch defaults false

        var overridden = await handler.Handle(
            new MergeRoundTripCommand(outbound.Id, overnightReturn.Id, AllowMismatch: true), CancellationToken.None);

        Assert.True(overridden.IsSuccess);
        Assert.Equal(TripDirection.Outbound, outbound.Direction);
        Assert.Equal(TripDirection.Inbound, overnightReturn.Direction);
        Assert.Equal(outbound.RoundTripKey, overnightReturn.RoundTripKey);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Manifest_declared_direction_outranks_the_chronological_fallback()
    {
        // The earlier-departing leg would be Outbound chronologically, but its manifest
        // says Inbound — the manifest wins and the pairing flips.
        var (early, late) = AddMergeablePair();
        AttachManifest(early, TripDirection.Inbound);
        var handler = MergeHandler();

        var result = await handler.Handle(
            new MergeRoundTripCommand(early.Id, late.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripDirection.Inbound, early.Direction);
        Assert.Equal(TripDirection.Outbound, late.Direction);
        Assert.Equal(early.RoundTripKey, late.RoundTripKey);
    }

    [Fact]
    public async Task Complementary_manifest_directions_assign_both_legs()
    {
        var (early, late) = AddMergeablePair();
        AttachManifest(early, TripDirection.Outbound);
        AttachManifest(late, TripDirection.Inbound);
        var handler = MergeHandler();

        var result = await handler.Handle(
            new MergeRoundTripCommand(late.Id, early.Id), CancellationToken.None); // argument order irrelevant

        Assert.True(result.IsSuccess);
        Assert.Equal(TripDirection.Outbound, early.Direction);
        Assert.Equal(TripDirection.Inbound, late.Direction);
    }

    [Fact]
    public async Task Same_declared_direction_on_both_manifests_refuses_the_merge()
    {
        var (early, late) = AddMergeablePair();
        AttachManifest(early, TripDirection.Outbound);
        AttachManifest(late, TripDirection.Outbound);
        var handler = MergeHandler();

        var result = await handler.Handle(
            new MergeRoundTripCommand(early.Id, late.Id), CancellationToken.None);

        Assert.Equal(TripErrors.RoundTripManifestDirectionConflict, result.Error);
        Assert.Null(early.RoundTripKey);
        Assert.Null(late.RoundTripKey);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Direction_less_manifests_fall_back_to_chronology()
    {
        var (early, late) = AddMergeablePair();
        AttachManifest(early, direction: null);
        AttachManifest(late, direction: null);
        var handler = MergeHandler();

        var result = await handler.Handle(
            new MergeRoundTripCommand(early.Id, late.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripDirection.Outbound, early.Direction);
        Assert.Equal(TripDirection.Inbound, late.Direction);
    }

    [Fact]
    public async Task Merge_reports_not_found_for_either_missing_trip()
    {
        var (outbound, _) = AddMergeablePair();
        var handler = MergeHandler();

        var missingOther = await handler.Handle(
            new MergeRoundTripCommand(outbound.Id, Guid.NewGuid()), CancellationToken.None);
        var missingTarget = await handler.Handle(
            new MergeRoundTripCommand(Guid.NewGuid(), outbound.Id), CancellationToken.None);

        Assert.Equal(TripErrors.NotFound, missingOther.Error);
        Assert.Equal(TripErrors.NotFound, missingTarget.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    /// <summary>
    /// Drives a client leg out of the operational phase into ReadyForBilling — the console's
    /// "read-only" trip, and the case the reason guard exists for.
    /// </summary>
    private static void CloseOperationally(Trip trip)
    {
        Assert.True(trip.RecordPostTripInspection().IsSuccess);
        Assert.True(trip.FinishOperations().IsSuccess);
        Assert.True(trip.IsOperationallyClosed);
    }

    [Fact]
    public async Task Merge_touching_a_closed_leg_without_a_reason_is_refused()
    {
        var (outbound, inbound) = AddMergeablePair();
        CloseOperationally(inbound);
        var handler = MergeHandler();

        var result = await handler.Handle(
            new MergeRoundTripCommand(outbound.Id, inbound.Id), CancellationToken.None);

        Assert.Equal(TripErrors.RoundTripReasonRequired, result.Error);
        Assert.Null(outbound.RoundTripKey);
        Assert.Null(inbound.RoundTripKey);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Whitespace_is_not_a_reason_for_a_closed_leg()
    {
        var (outbound, inbound) = AddMergeablePair();
        CloseOperationally(inbound);
        var handler = MergeHandler();

        var result = await handler.Handle(
            new MergeRoundTripCommand(outbound.Id, inbound.Id, Reason: "   "), CancellationToken.None);

        Assert.Equal(TripErrors.RoundTripReasonRequired, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Merge_touching_a_closed_leg_with_a_reason_pairs_both_legs()
    {
        // The domain still permits this — only Cancelled/WrittenOff are refused. The reason is
        // the added requirement, and it is the handler's, not the aggregate's.
        var (outbound, inbound) = AddMergeablePair();
        CloseOperationally(inbound);
        var handler = MergeHandler();

        var result = await handler.Handle(
            new MergeRoundTripCommand(
                outbound.Id, inbound.Id, Reason: "Client confirmed both legs were one booking."),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(outbound.RoundTripKey);
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
        Assert.Equal(TripStatus.ReadyForBilling, inbound.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Merge_of_two_open_legs_needs_no_reason()
    {
        var (outbound, inbound) = AddMergeablePair();
        var handler = MergeHandler();

        var result = await handler.Handle(
            new MergeRoundTripCommand(outbound.Id, inbound.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task A_reason_over_500_characters_is_refused()
    {
        var (outbound, inbound) = AddMergeablePair();
        CloseOperationally(inbound);
        var handler = MergeHandler();

        var result = await handler.Handle(
            new MergeRoundTripCommand(outbound.Id, inbound.Id, Reason: new string('x', 501)),
            CancellationToken.None);

        Assert.Equal(TripErrors.RoundTripReasonTooLong, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Unpair_clears_both_legs_of_the_pair()
    {
        var (outbound, inbound) = AddMergeablePair();
        Trip.MergeRoundTrip(outbound, inbound);
        var handler = new UnpairRoundTripCommandHandler(_trips);

        var result = await handler.Handle(new UnpairRoundTripCommand(inbound.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(outbound.RoundTripKey);
        Assert.Null(outbound.Direction);
        Assert.Null(inbound.RoundTripKey);
        Assert.Null(inbound.Direction);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Unpair_of_an_unpaired_trip_is_refused()
    {
        var (outbound, _) = AddMergeablePair();
        var handler = new UnpairRoundTripCommandHandler(_trips);

        var result = await handler.Handle(new UnpairRoundTripCommand(outbound.Id), CancellationToken.None);

        Assert.Equal(TripErrors.RoundTripNotPaired, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    private readonly FakeDriverLookupRepository _drivers = new();
    private readonly FakeVehicleLookupRepository _vehicles = new();

    private CreateDeadheadReturnCommandHandler DeadheadHandler() =>
        new(_trips, _drivers, _vehicles, new FakeTripNumberGenerator());

    private static CreateDeadheadReturnCommand DeadheadCommand(
        Guid tripId, Guid? driverId = null, Guid? vehicleId = null) =>
        new(tripId, driverId, vehicleId);

    [Fact]
    public async Task Deadhead_return_mints_a_trip_number_adds_the_new_leg_and_returns_its_id()
    {
        var source = TestPlanning.ScheduleTrip(tripNumber: "TR-2001", clientId: ClientId).Value;
        _trips.Add(source);

        var result = await DeadheadHandler().Handle(DeadheadCommand(source.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _trips.Trips.Count);
        var deadhead = Assert.Single(_trips.Trips, t => t.Id == result.Value);
        Assert.Equal("TR-9001", deadhead.TripNumber);
        Assert.True(deadhead.IsEmptyLeg);
        Assert.Equal(source.RoundTripKey, deadhead.RoundTripKey);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Deadhead_return_inherits_the_source_trips_driver_and_vehicle_by_default()
    {
        var source = TestPlanning.ScheduleTrip(tripNumber: "TR-2001", clientId: ClientId).Value;
        _trips.Add(source);

        var result = await DeadheadHandler().Handle(DeadheadCommand(source.Id), CancellationToken.None);

        var deadhead = Assert.Single(_trips.Trips, t => t.Id == result.Value);
        Assert.Equal(source.DriverId, deadhead.DriverId);
        Assert.Equal(source.DriverName, deadhead.DriverName);
        Assert.Equal(source.VehicleId, deadhead.VehicleId);
        Assert.Equal(source.VehicleUnit, deadhead.VehicleUnit);
    }

    [Fact]
    public async Task Deadhead_return_takes_a_different_driver_and_unit_when_supplied()
    {
        var source = TestPlanning.ScheduleTrip(tripNumber: "TR-2001", clientId: ClientId).Value;
        _trips.Add(source);
        var otherDriver = new DriverLookup
        {
            DriverId = Guid.NewGuid(),
            TenantId = TestPlanning.TenantId,
            Name = "S. Moody",
            LicenceClass = "Class 4",
            Status = DriverLookup.ActiveStatus,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        var otherVehicle = new VehicleLookup
        {
            VehicleId = Guid.NewGuid(),
            TenantId = TestPlanning.TenantId,
            UnitNumber = "U-07",
            Status = VehicleLookup.ActiveStatus,
            RequiredLicenceClass = "Class 4",
            SeatingCapacity = 14,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        _drivers.Drivers.Add(otherDriver);
        _vehicles.Vehicles.Add(otherVehicle);

        var result = await DeadheadHandler().Handle(
            DeadheadCommand(source.Id, otherDriver.DriverId, otherVehicle.VehicleId), CancellationToken.None);

        var deadhead = Assert.Single(_trips.Trips, t => t.Id == result.Value);
        Assert.Equal(otherDriver.DriverId, deadhead.DriverId);
        Assert.Equal("S. Moody", deadhead.DriverName);
        Assert.Equal(otherVehicle.VehicleId, deadhead.VehicleId);
        Assert.Equal("U-07", deadhead.VehicleUnit);
    }

    [Fact]
    public async Task Deadhead_return_with_an_unknown_override_driver_is_refused()
    {
        var source = TestPlanning.ScheduleTrip(tripNumber: "TR-2001", clientId: ClientId).Value;
        _trips.Add(source);

        var result = await DeadheadHandler().Handle(
            DeadheadCommand(source.Id, driverId: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TripErrors.DriverNotFound, result.Error);
        Assert.Single(_trips.Trips);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Deadhead_return_of_an_unassigned_source_requires_the_assignment()
    {
        // A source can lose its driver after creation (reassignment mid-plan) — someone
        // still has to drive the unit back, so the command is refused until a driver
        // override is supplied.
        var source = TestPlanning.ScheduleTrip(tripNumber: "TR-2001", clientId: ClientId).Value;
        Assert.True(source.UnassignDriver().IsSuccess);
        _trips.Add(source);

        var result = await DeadheadHandler().Handle(DeadheadCommand(source.Id), CancellationToken.None);

        Assert.Equal(TripErrors.DriverRequired, result.Error);
        Assert.Single(_trips.Trips);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Deadhead_return_for_a_missing_trip_reports_not_found()
    {
        var result = await DeadheadHandler().Handle(DeadheadCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TripErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task A_refused_deadhead_source_adds_nothing_and_saves_nothing()
    {
        var communityRun = TestPlanning.ScheduleTrip(tripNumber: "TR-2001", clientId: null).Value;
        _trips.Add(communityRun);

        var result = await DeadheadHandler().Handle(DeadheadCommand(communityRun.Id), CancellationToken.None);

        Assert.Equal(TripErrors.RoundTripClientRequired, result.Error);
        Assert.Single(_trips.Trips);
        Assert.Equal(0, _trips.SaveCount);
    }

    private sealed class FakeTripNumberGenerator : ITripNumberGenerator
    {
        private int _next = 9000;

        public Task<string> NextAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult($"TR-{++_next}");
    }
}

using NorthernLink.Trips.Application.Routes;
using NorthernLink.Trips.Application.Routes.Create;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Stops;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class CreateRouteCommandHandlerTests
{
    private readonly FakeStopRepository _stops = new();
    private readonly FakeRouteRepository _routes = new();

    private CreateRouteCommandHandler Handler => new(_routes, _stops);

    /// <summary>Seeds an active catalog stop with a distinct Manitoba coordinate and returns it.</summary>
    private Stop SeedStop(string name, double latitude, double longitude)
    {
        var stop = Stop.Create(
            TestPlanning.TenantId,
            name,
            StopType.Community,
            Address.Create(null, name, "Manitoba", null, "Canada").Value,
            Coordinate.Create(latitude, longitude).Value,
            notes: null).Value;
        _stops.Add(stop);
        return stop;
    }

    /// <summary>An untimed route over the given stops — the shape every route had before timetables.</summary>
    private static CreateRouteCommand Command(params Guid[] stopIds) =>
        Command([.. stopIds.Select(id => new RouteStopInput(id))]);

    private static CreateRouteCommand Command(IReadOnlyList<RouteStopInput> stops) =>
        new(
            TestPlanning.TenantId,
            "Thompson ↔ Lynn Lake",
            stops,
            DistanceKm: 320,
            EstimatedDurationMinutes: 105,
            RequiredLicenceClass: "Class 4");

    [Fact]
    public async Task Success_snapshots_each_stop_in_command_order_and_saves_the_route()
    {
        var thompson = SeedStop("Thompson", 55.74, -97.85);
        var leafRapids = SeedStop("Leaf Rapids", 56.50, -99.98);
        var lynnLake = SeedStop("Lynn Lake", 56.85, -101.05);

        // Deliberately not creation order — proves Order follows the StopIds list.
        var result = await Handler.Handle(
            Command(lynnLake.Id, thompson.Id, leafRapids.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var route = Assert.IsType<Route>(_routes.Added);
        Assert.Equal(result.Value, route.Id);
        Assert.Equal(1, _routes.SaveCount);

        var ordered = route.Stops.OrderBy(s => s.Order).ToList();
        Assert.Equal(3, ordered.Count);

        Assert.Equal(0, ordered[0].Order);
        Assert.Equal("Lynn Lake", ordered[0].Name);
        Assert.Equal(lynnLake.Id, ordered[0].StopId);
        Assert.Equal(56.85, ordered[0].Latitude);
        Assert.Equal(-101.05, ordered[0].Longitude);

        Assert.Equal(1, ordered[1].Order);
        Assert.Equal("Thompson", ordered[1].Name);
        Assert.Equal(thompson.Id, ordered[1].StopId);
        Assert.Equal(55.74, ordered[1].Latitude);
        Assert.Equal(-97.85, ordered[1].Longitude);

        Assert.Equal(2, ordered[2].Order);
        Assert.Equal("Leaf Rapids", ordered[2].Name);
        Assert.Equal(leafRapids.Id, ordered[2].StopId);
        Assert.Equal(56.50, ordered[2].Latitude);
        Assert.Equal(-99.98, ordered[2].Longitude);
    }

    [Fact]
    public async Task Unknown_stop_id_is_rejected_and_the_route_is_not_added()
    {
        var thompson = SeedStop("Thompson", 55.74, -97.85);
        var missingId = Guid.NewGuid();

        var result = await Handler.Handle(
            Command(thompson.Id, missingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RouteErrors.UnknownStop, result.Error);
        Assert.Null(_routes.Added);
        Assert.Equal(0, _routes.SaveCount);
    }

    [Fact]
    public async Task Inactive_stop_is_rejected_and_the_route_is_not_added()
    {
        var thompson = SeedStop("Thompson", 55.74, -97.85);
        var lynnLake = SeedStop("Lynn Lake", 56.85, -101.05);
        lynnLake.SetActive(false);

        var result = await Handler.Handle(
            Command(thompson.Id, lynnLake.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RouteErrors.InactiveStop, result.Error);
        Assert.Null(_routes.Added);
        Assert.Equal(0, _routes.SaveCount);
    }

    // ---- Timetable ----------------------------------------------------------------
    // Offsets are minutes after the leg's own departure. The outbound leg travels the stop
    // list forwards; the return travels it backwards, so the LAST stop is the return's zero.

    /// <summary>Seeds the three-stop Thompson → Leaf Rapids → Lynn Lake corridor, in order.</summary>
    private (Guid Thompson, Guid LeafRapids, Guid LynnLake) SeedCorridor() =>
        (SeedStop("Thompson", 55.74, -97.85).Id,
         SeedStop("Leaf Rapids", 56.50, -99.98).Id,
         SeedStop("Lynn Lake", 56.85, -101.05).Id);

    [Fact]
    public async Task Timetable_offsets_are_snapshotted_onto_each_stop()
    {
        var (thompson, leafRapids, lynnLake) = SeedCorridor();

        var result = await Handler.Handle(
            Command([
                new RouteStopInput(thompson, OutboundOffsetMinutes: 0, ReturnOffsetMinutes: 235),
                new RouteStopInput(leafRapids, OutboundOffsetMinutes: 95, ReturnOffsetMinutes: 85),
                new RouteStopInput(lynnLake, OutboundOffsetMinutes: 240, ReturnOffsetMinutes: 0),
            ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var ordered = _routes.Added!.Stops.OrderBy(s => s.Order).ToList();
        Assert.Equal([0, 95, 240], ordered.Select(s => s.OutboundOffsetMinutes));

        // The return's zero sits on the last stop — it starts where the outbound ended.
        Assert.Equal([235, 85, 0], ordered.Select(s => s.ReturnOffsetMinutes));
    }

    [Fact]
    public async Task A_route_with_no_timetable_is_still_valid_and_leaves_both_offsets_null()
    {
        var (thompson, leafRapids, lynnLake) = SeedCorridor();

        var result = await Handler.Handle(
            Command(thompson, leafRapids, lynnLake), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(_routes.Added!.Stops, stop =>
        {
            Assert.Null(stop.OutboundOffsetMinutes);
            Assert.Null(stop.ReturnOffsetMinutes);
        });
    }

    [Fact]
    public async Task One_leg_may_be_timed_while_the_other_is_not()
    {
        var (thompson, leafRapids, lynnLake) = SeedCorridor();

        var result = await Handler.Handle(
            Command([
                new RouteStopInput(thompson, OutboundOffsetMinutes: 0),
                new RouteStopInput(leafRapids, OutboundOffsetMinutes: 95),
                new RouteStopInput(lynnLake, OutboundOffsetMinutes: 240),
            ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(_routes.Added!.Stops, stop => Assert.Null(stop.ReturnOffsetMinutes));
    }

    [Fact]
    public async Task A_partially_timed_leg_is_rejected()
    {
        var (thompson, leafRapids, lynnLake) = SeedCorridor();

        // Leaf Rapids has no time — it would silently fall back to the trip's departure and
        // tell a mid-corridor passenger to be ready 95 minutes early.
        var result = await Handler.Handle(
            Command([
                new RouteStopInput(thompson, OutboundOffsetMinutes: 0),
                new RouteStopInput(leafRapids),
                new RouteStopInput(lynnLake, OutboundOffsetMinutes: 240),
            ]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RouteErrors.PartialTimetable, result.Error);
        Assert.Null(_routes.Added);
    }

    [Fact]
    public async Task A_leg_that_does_not_start_at_zero_is_rejected()
    {
        var (thompson, leafRapids, lynnLake) = SeedCorridor();

        var result = await Handler.Handle(
            Command([
                new RouteStopInput(thompson, OutboundOffsetMinutes: 30),
                new RouteStopInput(leafRapids, OutboundOffsetMinutes: 95),
                new RouteStopInput(lynnLake, OutboundOffsetMinutes: 240),
            ]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RouteErrors.TimetableMustStartAtZero, result.Error);
    }

    [Fact]
    public async Task A_leg_whose_offsets_do_not_increase_is_rejected()
    {
        var (thompson, leafRapids, lynnLake) = SeedCorridor();

        var result = await Handler.Handle(
            Command([
                new RouteStopInput(thompson, OutboundOffsetMinutes: 0),
                new RouteStopInput(leafRapids, OutboundOffsetMinutes: 240),
                new RouteStopInput(lynnLake, OutboundOffsetMinutes: 95),
            ]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RouteErrors.TimetableNotIncreasing, result.Error);
    }

    [Fact]
    public async Task The_return_leg_is_validated_backwards_along_the_stop_list()
    {
        var (thompson, leafRapids, lynnLake) = SeedCorridor();

        // Ascending with the list — which is exactly BACKWARDS for a return leg, whose zero
        // belongs on the last stop. Rejected, though the same numbers are a valid outbound.
        var result = await Handler.Handle(
            Command([
                new RouteStopInput(thompson, ReturnOffsetMinutes: 0),
                new RouteStopInput(leafRapids, ReturnOffsetMinutes: 85),
                new RouteStopInput(lynnLake, ReturnOffsetMinutes: 235),
            ]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RouteErrors.TimetableMustStartAtZero, result.Error);
    }
}

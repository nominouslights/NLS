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

    private static CreateRouteCommand Command(params Guid[] stopIds) =>
        new(
            TestPlanning.TenantId,
            "Thompson ↔ Lynn Lake",
            stopIds,
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
}

using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Tests;

/// <summary>Builders for the route-change tests: catalogue routes with real stop ids, and trips snapshotted from them.</summary>
internal static class TestRouteChange
{
    private static readonly Dictionary<string, Guid> StopIds = new(StringComparer.Ordinal);

    /// <summary>A stable catalogue stop id per name, so two routes sharing a town share its id.</summary>
    public static Guid StopId(string name)
    {
        lock (StopIds)
        {
            if (!StopIds.TryGetValue(name, out var id))
            {
                id = Guid.NewGuid();
                StopIds[name] = id;
            }

            return id;
        }
    }

    /// <summary>A catalogue route over <paramref name="names"/> with both timetables, 105 min end to end.</summary>
    public static Route CreateRoute(int durationMinutes, params string[] names) =>
        Route.Create(
            TestPlanning.TenantId,
            $"{names[0]} ↔ {names[^1]}",
            [.. names.Select((name, index) => new RouteStop
            {
                StopId = StopId(name),
                Name = name,
                Order = index,
                OutboundOffsetMinutes = index * 30,
                ReturnOffsetMinutes = (names.Length - 1 - index) * 40,
            })],
            distanceKm: 100 * names.Length,
            estimatedDuration: TimeSpan.FromMinutes(durationMinutes),
            requiredLicenceClass: null).Value;

    public static Route CreateRoute(params string[] names) => CreateRoute(105, names);

    /// <summary>A Scheduled trip snapshotted from <paramref name="route"/> for <paramref name="direction"/>, as generation builds it.</summary>
    public static Trip TripOn(
        Route route,
        TripDirection? direction,
        string tripNumber = "TR-1001",
        string? roundTripKey = null,
        Guid? scheduleTemplateId = null,
        TimeOnly? windowStart = null,
        bool openEnded = false)
    {
        var inbound = direction == TripDirection.Inbound;
        var start = windowStart ?? (inbound ? new TimeOnly(16, 0) : new TimeOnly(6, 30));
        var trip = Trip.Schedule(
            TestPlanning.TenantId,
            tripNumber,
            serviceDate: new DateOnly(2026, 7, 21),
            windowStart: start,
            windowEnd: openEnded ? null : start.Add(route.EstimatedDuration),
            TripServiceType.ContractCrew,
            routeId: route.Id,
            routeName: route.Name,
            origin: inbound ? route.Destination : route.Origin,
            destination: inbound ? route.Origin : route.Destination,
            stops: RouteStop.OrientedFor(route.Stops, direction),
            distanceKm: route.DistanceKm,
            scheduleTemplateId: scheduleTemplateId,
            roundTripKey: roundTripKey,
            direction: direction,
            isEmptyLeg: false,
            clientId: Guid.Parse("00000000-0000-0000-0000-0000000000c1"),
            clientName: "Alamos Gold",
            poNumber: "PO-OLD",
            driverId: TestPlanning.DriverId,
            driverName: TestPlanning.DriverName,
            vehicleId: TestPlanning.VehicleId,
            vehicleUnit: TestPlanning.VehicleUnit,
            seatsCapacity: 12,
            seatsMinimum: 4).Value;
        trip.ClearDomainEvents();
        return trip;
    }

    /// <summary>A passenger picked up and dropped off at the given stops (by catalogue id + name, as the editor saves them).</summary>
    public static ManifestPassenger Passenger(string name, string? pickup, string? dropoff) => new()
    {
        Name = name,
        PickupStopId = pickup is null ? null : StopId(pickup),
        PickupStopName = pickup,
        DropoffStopId = dropoff is null ? null : StopId(dropoff),
        DropoffStopName = dropoff,
    };

    /// <summary>A dispatcher-entered manifest for <paramref name="trip"/>, linked to it.</summary>
    public static TripManifest ManifestFor(Trip trip, params ManifestPassenger[] passengers)
    {
        var manifest = TripManifest.Create(
            TestPlanning.TenantId,
            trip.ServiceDate,
            trip.TripNumber,
            trip.RouteName,
            trip.Direction,
            trip.ClientName,
            passengers,
            allSeatbeltsVerified: false,
            cargo: [],
            allCargoSecured: null,
            ManifestSource.Dispatcher,
            enteredBy: "dispatch@northernlink.ca").Value;
        manifest.ClearDomainEvents();
        trip.AttachManifest(manifest.Id);
        trip.ClearDomainEvents();
        return manifest;
    }
}

/// <summary>Route repository holding any number of routes (the shared fake keeps only the last one added).</summary>
internal sealed class InMemoryRouteRepository : IRouteRepository
{
    public List<Route> Routes { get; } = [];

    public void Add(Route route) => Routes.Add(route);

    public Task<Route?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Routes.FirstOrDefault(r => r.Id == routeId));

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

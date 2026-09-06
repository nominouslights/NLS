using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Shared.IntegrationEvents.Booking;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The chain-reaction consumer: booking.booking-day-confirmed → a Community trip with no
/// driver/vehicle, provisional 08:00 window, route snapshot from RouteId (= CorridorId),
/// and DB-atomic idempotency on duplicate delivery.
/// </summary>
public class BookingDayConfirmedIntegrationEventHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BookingDayId = Guid.NewGuid();

    private readonly FakeTripRepository _trips = new();
    private readonly FakeRouteRepository _routes = new();
    private readonly Route _route;

    public BookingDayConfirmedIntegrationEventHandlerTests()
    {
        _route = Route.Create(
            TenantId,
            "Thompson ↔ Lynn Lake",
            [new RouteStop { Name = "Thompson", Order = 0 }, new RouteStop { Name = "Lynn Lake", Order = 1 }],
            320,
            TimeSpan.FromHours(4),
            requiredLicenceClass: null).Value;
        _routes.Add(_route);
    }

    private BookingDayConfirmedIntegrationEvent Event(Guid? corridorId = null) => new(
        BookingDayId,
        TenantId,
        corridorId ?? _route.Id,
        "Thompson ↔ Lynn Lake",
        "Thompson",
        "Lynn Lake",
        new DateOnly(2026, 9, 15),
        SeatsSold: 3,
        SeatsMinimum: 3,
        SeatCapacity: 7);

    private BookingDayConfirmedIntegrationEventHandler Handler(ITripRepository? trips = null) => new(
        trips ?? _trips,
        _routes,
        new FakeTripNumberGenerator(),
        NullLogger<BookingDayConfirmedIntegrationEventHandler>.Instance);

    [Fact]
    public async Task A_confirmation_creates_the_community_trip_from_the_route_snapshot()
    {
        await Handler().Handle(Event(), CancellationToken.None);

        var trip = Assert.Single(_trips.Trips);
        Assert.Equal(TripServiceType.Community, trip.ServiceType);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Null(trip.DriverId);
        Assert.Null(trip.VehicleId);
        Assert.Equal(BookingDayId, trip.BookingDayId);
        Assert.Equal(BookingDayConfirmedIntegrationEventHandler.ProvisionalWindowStart, trip.WindowStart);
        Assert.Equal(new DateOnly(2026, 9, 15), trip.ServiceDate);
        Assert.Equal(_route.Id, trip.RouteId);
        Assert.Equal("Thompson ↔ Lynn Lake", trip.RouteName);
        Assert.Equal("Thompson", trip.Origin);
        Assert.Equal("Lynn Lake", trip.Destination);
        Assert.Equal(2, trip.Stops.Count);
        Assert.Equal(320, trip.DistanceKm);
        Assert.Equal(3, trip.SeatsConfirmed);
        Assert.Equal(7, trip.SeatsCapacity);
        Assert.Equal(3, trip.SeatsMinimum);
        Assert.Equal("NL-9001", trip.TripNumber);
    }

    [Fact]
    public async Task A_duplicate_event_creates_no_second_trip()
    {
        var handler = Handler();

        await handler.Handle(Event(), CancellationToken.None);
        await handler.Handle(Event(), CancellationToken.None);

        Assert.Single(_trips.Trips);
    }

    [Fact]
    public async Task A_reconfirmation_after_a_revert_reuses_the_existing_trip()
    {
        var handler = Handler();
        await handler.Handle(Event(), CancellationToken.None);
        var first = Assert.Single(_trips.Trips);

        // The day reverts, recovers, and re-confirms: a NEW event id, same booking day.
        await handler.Handle(Event(), CancellationToken.None);

        var still = Assert.Single(_trips.Trips);
        Assert.Same(first, still);
    }

    [Fact]
    public async Task A_lost_pre_check_race_falls_back_to_the_unique_index_and_noops()
    {
        // Simulate two instances racing: the pre-check sees nothing, but the (tenant,
        // booking day) unique index rejects the insert. TryAdd's false must end as a no-op.
        var racing = new RacingTripRepository(_trips);
        await Handler(_trips).Handle(Event(), CancellationToken.None);

        await Handler(racing).Handle(Event(), CancellationToken.None);

        Assert.Single(_trips.Trips);
    }

    [Fact]
    public async Task A_missing_route_is_dropped_without_creating_anything()
    {
        await Handler().Handle(Event(corridorId: Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(_trips.Trips);
    }

    private sealed class FakeTripNumberGenerator : ITripNumberGenerator
    {
        private int _next = 9001;

        public Task<string> NextAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult($"NL-{_next++}");
    }

    /// <summary>Delegates to the shared fake but blinds the pre-check, forcing the 23505 path.</summary>
    private sealed class RacingTripRepository(FakeTripRepository inner) : ITripRepository
    {
        public void Add(Trip trip) => inner.Add(trip);

        public Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
            inner.GetByIdAsync(tripId, cancellationToken);

        public Task<Trip?> GetByTripNumberAsync(string tripNumber, CancellationToken cancellationToken = default) =>
            inner.GetByTripNumberAsync(tripNumber, cancellationToken);

        public Task<IReadOnlyList<Trip>> GetByRoundTripKeyAsync(
            string roundTripKey, CancellationToken cancellationToken = default) =>
            inner.GetByRoundTripKeyAsync(roundTripKey, cancellationToken);

        public Task<IReadOnlyList<Trip>> GetByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> tripIds, CancellationToken cancellationToken = default) =>
            inner.GetByIdsAsync(tenantId, tripIds, cancellationToken);

        public Task<Trip?> GetByBookingDayIdAsync(
            Guid tenantId, Guid bookingDayId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Trip?>(null);

        public Task<bool> TryAddForBookingDayAsync(Trip trip, CancellationToken cancellationToken = default) =>
            inner.TryAddForBookingDayAsync(trip, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            inner.SaveChangesAsync(cancellationToken);
    }
}

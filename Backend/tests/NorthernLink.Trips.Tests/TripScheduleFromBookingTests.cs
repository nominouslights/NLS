using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The booking-sourced creation path: <see cref="Trip.ScheduleFromBooking"/>'s deliberate,
/// scoped relaxation of "never born unassigned" (Community only, no driver/vehicle), and
/// the mapper entry that publishes the backlink event Booking consumes.
/// </summary>
public class TripScheduleFromBookingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BookingDayId = Guid.NewGuid();
    private static readonly Guid RouteId = Guid.NewGuid();

    private static Result<Trip> Create(
        string tripNumber = "NL-2001",
        Guid? bookingDayId = null,
        int seatsConfirmed = 3) =>
        Trip.ScheduleFromBooking(
            TenantId,
            tripNumber,
            bookingDayId ?? BookingDayId,
            new DateOnly(2026, 9, 15),
            new TimeOnly(8, 0),
            RouteId,
            "Thompson ↔ Lynn Lake",
            "Thompson",
            "Lynn Lake",
            [new RouteStop { Name = "Thompson", Order = 0 }, new RouteStop { Name = "Lynn Lake", Order = 1 }],
            320,
            seatsConfirmed,
            seatsCapacity: 7,
            seatsMinimum: 3);

    [Fact]
    public void A_booking_trip_is_community_scheduled_and_unassigned()
    {
        var trip = Create().Value;

        Assert.Equal(TripServiceType.Community, trip.ServiceType);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Null(trip.DriverId);
        Assert.Null(trip.DriverName);
        Assert.Null(trip.VehicleId);
        Assert.Null(trip.VehicleUnit);
        Assert.Equal(BookingDayId, trip.BookingDayId);
        Assert.Equal(new TimeOnly(8, 0), trip.WindowStart);
        Assert.Equal(3, trip.SeatsConfirmed);
        Assert.Equal(7, trip.SeatsCapacity);
        Assert.Equal(3, trip.SeatsMinimum);
        Assert.Equal(RouteId, trip.RouteId);
    }

    [Fact]
    public void The_factory_raises_the_booking_sourced_event_only()
    {
        var trip = Create().Value;

        var raised = Assert.Single(trip.DomainEvents);
        Assert.IsType<TripScheduledFromBookingDomainEvent>(raised);
    }

    [Fact]
    public void Validation_still_applies()
    {
        Assert.Equal(TripErrors.TripNumberRequired, Create(tripNumber: " ").Error);
        Assert.Equal(TripErrors.BookingDayRequired, Create(bookingDayId: Guid.Empty).Error);
        Assert.Equal(TripErrors.InvalidSeats, Create(seatsConfirmed: -1).Error);
    }

    [Fact]
    public void An_unassigned_booking_trip_can_still_be_covered_later()
    {
        var trip = Create().Value;

        Assert.True(trip.AssignDriver(Guid.NewGuid(), "Ray Castel").IsSuccess);
        Assert.True(trip.AssignVehicle(Guid.NewGuid(), "U-12", 7).IsSuccess);
        Assert.Equal(7, trip.SeatsCapacity);
    }

    [Fact]
    public void The_mapper_publishes_the_backlink_event()
    {
        var trip = Create().Value;
        var mapper = new TripsIntegrationEventMapper();

        var mapped = mapper.Map(trip.DomainEvents.Single(), trip);

        var integrationEvent = Assert.IsType<TripScheduledFromBookingIntegrationEvent>(mapped);
        Assert.Equal(trip.Id, integrationEvent.TripId);
        Assert.Equal("NL-2001", integrationEvent.TripNumber);
        Assert.Equal(TenantId, integrationEvent.TenantId);
        Assert.Equal(BookingDayId, integrationEvent.BookingDayId);
        Assert.Equal(new DateOnly(2026, 9, 15), integrationEvent.ServiceDate);
    }
}

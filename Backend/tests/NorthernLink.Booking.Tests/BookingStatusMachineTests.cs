using NorthernLink.Booking.Domain.Bookings;
using NorthernLink.Booking.Domain.Bookings.Events;
using Xunit;
using BookingAggregate = NorthernLink.Booking.Domain.Bookings.Booking;

namespace NorthernLink.Booking.Tests;

/// <summary>
/// The booking status machine: Unconfirmed → Confirmed (only), {Unconfirmed, Confirmed} →
/// Cancelled, Cancelled terminal + read-only.
/// </summary>
public class BookingStatusMachineTests
{
    [Fact]
    public void New_booking_starts_unconfirmed_and_unpaid_with_hold_stamped()
    {
        var before = DateTimeOffset.UtcNow;
        var booking = TestBookings.Create(seatHold: TimeSpan.FromMinutes(30));
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(BookingStatus.Unconfirmed, booking.Status);
        Assert.Equal(PaymentStatus.Unpaid, booking.PaymentStatus);
        Assert.InRange(
            booking.HoldExpiresAtUtc,
            before.AddMinutes(30),
            after.AddMinutes(30));
        Assert.IsType<BookingCreatedDomainEvent>(Assert.Single(booking.DomainEvents));
    }

    [Fact]
    public void Confirm_from_unconfirmed_succeeds()
    {
        var booking = TestBookings.Create();
        booking.ClearDomainEvents();

        var result = booking.Confirm();

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.IsType<BookingConfirmedDomainEvent>(Assert.Single(booking.DomainEvents));
    }

    [Fact]
    public void Confirm_when_already_confirmed_is_rejected()
    {
        var booking = TestBookings.Create();
        booking.Confirm();

        var result = booking.Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.AlreadyConfirmed, result.Error);
    }

    [Fact]
    public void Confirm_when_cancelled_is_rejected()
    {
        var booking = TestBookings.Create();
        booking.Cancel();

        var result = booking.Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.CancelledIsReadOnly, result.Error);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public void Cancel_from_unconfirmed_succeeds()
    {
        var booking = TestBookings.Create();
        booking.ClearDomainEvents();

        var result = booking.Cancel();

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.IsType<BookingCancelledDomainEvent>(Assert.Single(booking.DomainEvents));
    }

    [Fact]
    public void Cancel_from_confirmed_succeeds()
    {
        var booking = TestBookings.Create();
        booking.Confirm();

        var result = booking.Cancel();

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public void Cancel_when_already_cancelled_is_rejected()
    {
        var booking = TestBookings.Create();
        booking.Cancel();

        var result = booking.Cancel();

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.AlreadyCancelled, result.Error);
    }

    [Fact]
    public void Update_when_cancelled_is_rejected_and_state_unchanged()
    {
        var booking = TestBookings.Create(passengerCount: 2);
        booking.Cancel();

        var result = booking.Update(
            TestBookings.Location("Somewhere Else"),
            TestBookings.Location("Another Place"),
            [new BookingPassengerDetails("New Passenger", null, false)],
            PaymentMethod.Cash,
            PaymentStatus.Paid,
            "edited");

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.CancelledIsReadOnly, result.Error);
        Assert.Equal(2, booking.Passengers.Count);
        Assert.Equal(PaymentStatus.Unpaid, booking.PaymentStatus);
        Assert.Null(booking.Notes);
    }

    [Fact]
    public void Update_while_confirmed_is_allowed()
    {
        var booking = TestBookings.Create();
        booking.Confirm();

        var result = booking.Update(
            booking.Pickup,
            booking.Dropoff,
            [new BookingPassengerDetails("Solo Traveller", "204-555-0101", true)],
            PaymentMethod.Square,
            PaymentStatus.Paid,
            "paid at depot");

        Assert.True(result.IsSuccess);
        var passenger = Assert.Single(booking.Passengers);
        Assert.Equal("Solo Traveller", passenger.Name);
        Assert.Equal(PaymentStatus.Paid, booking.PaymentStatus);
    }

    [Fact]
    public void Create_requires_at_least_one_passenger()
    {
        var result = BookingAggregate.Create(
            TestBookings.TenantId,
            TestBookings.CustomerId,
            "Doris Spence",
            TestBookings.CorridorId,
            "Thompson ↔ Lynn Lake",
            new DateOnly(2026, 9, 15),
            TestBookings.Location(),
            TestBookings.Location("Lynn Lake Terminal"),
            [],
            PaymentMethod.Cash,
            TimeSpan.FromMinutes(30),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.AtLeastOnePassenger, result.Error);
    }

    [Fact]
    public void Create_requires_every_passenger_name()
    {
        var result = BookingAggregate.Create(
            TestBookings.TenantId,
            TestBookings.CustomerId,
            "Doris Spence",
            TestBookings.CorridorId,
            "Thompson ↔ Lynn Lake",
            new DateOnly(2026, 9, 15),
            TestBookings.Location(),
            TestBookings.Location("Lynn Lake Terminal"),
            [new BookingPassengerDetails("Named", null, true), new BookingPassengerDetails("  ", null, false)],
            PaymentMethod.Cash,
            TimeSpan.FromMinutes(30),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.PassengerNameRequired, result.Error);
    }
}

using NorthernLink.Booking.Application.Bookings;
using NorthernLink.Booking.Application.Bookings.GetById;
using NorthernLink.Booking.Application.Customers;
using NorthernLink.Booking.Domain.Bookings;
using Xunit;

namespace NorthernLink.Booking.Tests;

/// <summary>The detail query: unknown (or another tenant's) id → NotFound; found → booking + customer.</summary>
public class GetBookingByIdQueryHandlerTests
{
    private readonly FakeBookingReadService _readService = new();

    private GetBookingByIdQueryHandler Handler() => new(_readService);

    [Fact]
    public async Task Unknown_booking_is_NotFound()
    {
        var result = await Handler().Handle(
            new GetBookingByIdQuery(TestBookings.TenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Found_booking_returns_the_booking_and_its_customer()
    {
        var bookingId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var booking = new BookingResponse(
            bookingId,
            "NL-7K3M2Q",
            TestBookings.CustomerId,
            "Doris Spence",
            TestBookings.CorridorId,
            "Thompson ↔ Lynn Lake",
            new DateOnly(2026, 9, 15),
            BookingStatus.Confirmed,
            new BookingLocationResponse(Guid.NewGuid(), "Thompson Depot", null),
            new BookingLocationResponse(Guid.NewGuid(), "Lynn Lake Terminal", null),
            [new BookingPassengerResponse(Guid.NewGuid(), "Doris Spence", "204-555-0199", true)],
            PaymentMethod.ETransfer,
            PaymentStatus.Unpaid,
            now.AddMinutes(30),
            HoldExpired: false,
            null,
            now,
            now);
        var customer = new CustomerResponse(
            TestBookings.CustomerId, "Doris Spence", "204-555-0199", "doris@example.com", null, now, now);
        _readService.Details[bookingId] = new BookingDetailResponse(booking, customer);

        var result = await Handler().Handle(
            new GetBookingByIdQuery(TestBookings.TenantId, bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("NL-7K3M2Q", result.Value.Booking.Reference);
        Assert.Equal(bookingId, result.Value.Booking.Id);
        Assert.NotNull(result.Value.Customer);
        Assert.Equal("doris@example.com", result.Value.Customer!.Email);
    }

    [Fact]
    public async Task Found_booking_with_a_vanished_customer_row_still_returns_the_booking()
    {
        var bookingId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var booking = new BookingResponse(
            bookingId, "NL-ABCDEF", TestBookings.CustomerId, "Doris Spence", TestBookings.CorridorId,
            "Thompson ↔ Lynn Lake", new DateOnly(2026, 9, 15), BookingStatus.Unconfirmed,
            new BookingLocationResponse(null, "Thompson Depot", null),
            new BookingLocationResponse(null, "Lynn Lake Terminal", null),
            [new BookingPassengerResponse(Guid.NewGuid(), "Doris Spence", null, true)],
            PaymentMethod.Cash, PaymentStatus.Unpaid, now.AddMinutes(30), HoldExpired: false, null, now, now);
        _readService.Details[bookingId] = new BookingDetailResponse(booking, Customer: null);

        var result = await Handler().Handle(
            new GetBookingByIdQuery(TestBookings.TenantId, bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Customer);
        Assert.Equal("NL-ABCDEF", result.Value.Booking.Reference);
    }
}

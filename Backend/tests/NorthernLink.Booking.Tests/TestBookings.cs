using NorthernLink.Booking.Domain.Bookings;
using BookingAggregate = NorthernLink.Booking.Domain.Bookings.Booking;

namespace NorthernLink.Booking.Tests;

/// <summary>Shared factories for valid aggregates — tests mutate from a known-good baseline.</summary>
internal static class TestBookings
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid CustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid CorridorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static BookingLocation Location(string stopName = "Thompson Depot") =>
        BookingLocation.Create(Guid.NewGuid(), stopName, null).Value;

    public static BookingAggregate Create(
        int passengerCount = 2,
        TimeSpan? seatHold = null,
        Guid? customerId = null,
        string customerName = "Doris Spence",
        DateOnly? serviceDate = null)
    {
        var passengers = Enumerable.Range(1, passengerCount)
            .Select(i => new BookingPassengerDetails($"Passenger {i}", null, i == 1))
            .ToList();

        return BookingAggregate.Create(
            TenantId,
            customerId ?? CustomerId,
            customerName,
            CorridorId,
            "Thompson ↔ Lynn Lake",
            serviceDate ?? new DateOnly(2026, 9, 15),
            Location(),
            Location("Lynn Lake Terminal"),
            passengers,
            PaymentMethod.ETransfer,
            seatHold ?? TimeSpan.FromMinutes(30),
            null).Value;
    }
}

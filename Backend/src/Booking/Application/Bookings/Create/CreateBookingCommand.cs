using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Bookings.Create;

public sealed record CreateBookingCommand(
    Guid TenantId,
    Guid CustomerId,
    Guid CorridorId,
    DateOnly ServiceDate,
    BookingLocationInput Pickup,
    BookingLocationInput Dropoff,
    IReadOnlyList<BookingPassengerInput> Passengers,
    PaymentMethod PaymentMethod,
    string? Notes) : ICommand<Guid>;

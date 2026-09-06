using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Bookings.Update;

/// <summary>
/// Edits a booking's details. Corridor, service date, status, and the seat hold are not
/// editable — cancel and rebook to move a booking. Rejected when the booking is Cancelled.
/// </summary>
public sealed record UpdateBookingCommand(
    Guid TenantId,
    Guid BookingId,
    BookingLocationInput Pickup,
    BookingLocationInput Dropoff,
    IReadOnlyList<BookingPassengerInput> Passengers,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    string? Notes) : ICommand;

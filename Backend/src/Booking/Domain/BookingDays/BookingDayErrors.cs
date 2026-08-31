using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.BookingDays;

/// <summary>All domain errors the BookingDay aggregate (and its handlers) can produce.</summary>
public static class BookingDayErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Booking.BookingDay.NotFound", "The booking day was not found.");

    public static readonly Error InvalidPassengerMinimum = Error.Validation(
        "Booking.BookingDay.InvalidPassengerMinimum", "A passenger-minimum override cannot be negative.");

    public static readonly Error InvalidSeatCapacity = Error.Validation(
        "Booking.BookingDay.InvalidSeatCapacity", "A seat-capacity override must be at least 1.");
}

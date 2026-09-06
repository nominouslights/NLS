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

    public static readonly Error NotConfirmed = Error.Conflict(
        "Booking.BookingDay.NotConfirmed", "Only a confirmed booking day can revert.");

    public static readonly Error MinimumGuaranteedSuppressesRevert = Error.Conflict(
        "Booking.BookingDay.MinimumGuaranteed",
        "The day's passenger minimum is guaranteed (Gift-a-Seat) — it never reverts.");

    public static readonly Error TripAlreadyLinked = Error.Conflict(
        "Booking.BookingDay.TripAlreadyLinked",
        "A different trip is already linked to this booking day.");

    public static readonly Error TripNumberRequired = Error.Validation(
        "Booking.BookingDay.TripNumberRequired", "Linking a trip requires its trip-number snapshot.");
}

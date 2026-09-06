using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.Bookings;

/// <summary>All domain errors the Booking aggregate (and its handlers) can produce.</summary>
public static class BookingErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Booking.Booking.NotFound", "The booking was not found.");

    public static readonly Error AtLeastOnePassenger = Error.Validation(
        "Booking.Booking.AtLeastOnePassenger", "A booking needs at least one passenger.");

    public static readonly Error PassengerNameRequired = Error.Validation(
        "Booking.Booking.PassengerNameRequired", "Every passenger needs a name.");

    public static readonly Error LocationStopNameRequired = Error.Validation(
        "Booking.Booking.LocationStopNameRequired", "A stop name is required when a stop is referenced.");

    public static readonly Error LocationRequired = Error.Validation(
        "Booking.Booking.LocationRequired", "A location needs a stop name or an address detail.");

    public static readonly Error CancelledIsReadOnly = Error.Conflict(
        "Booking.Booking.CancelledIsReadOnly", "A cancelled booking cannot be changed.");

    public static readonly Error AlreadyConfirmed = Error.Conflict(
        "Booking.Booking.AlreadyConfirmed", "The booking is already confirmed.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Booking.Booking.AlreadyCancelled", "The booking is already cancelled.");

    public static readonly Error CorridorNotFound = Error.NotFound(
        "Booking.Booking.CorridorNotFound",
        "The corridor was not found. Corridors are synced from Trips routes — re-save the route if it is missing.");
}

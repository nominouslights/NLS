using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips;

/// <summary>All domain errors the Trip aggregate (and its handlers) can produce.</summary>
public static class TripErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Trips.Trip.NotFound", "The trip was not found.");

    public static readonly Error TripNumberRequired = Error.Validation(
        "Trips.Trip.TripNumberRequired", "A trip number is required.");

    public static readonly Error RouteNameRequired = Error.Validation(
        "Trips.Trip.RouteNameRequired", "A route name is required.");

    public static readonly Error OriginAndDestinationRequired = Error.Validation(
        "Trips.Trip.OriginAndDestinationRequired", "An origin and a destination are required.");

    public static readonly Error InvalidDistance = Error.Validation(
        "Trips.Trip.InvalidDistance", "The trip distance cannot be negative.");

    public static readonly Error InvalidSeats = Error.Validation(
        "Trips.Trip.InvalidSeats", "Seat counts cannot be negative.");

    public static readonly Error SeatsExceedCapacity = Error.Validation(
        "Trips.Trip.SeatsExceedCapacity", "Confirmed seats cannot exceed the trip's seat capacity.");

    public static readonly Error DriverNameRequired = Error.Validation(
        "Trips.Trip.DriverNameRequired", "Assigning a driver requires the driver's name snapshot.");

    public static readonly Error DriverNotFound = Error.NotFound(
        "Trips.Trip.DriverNotFound", "The driver to assign was not found.");

    public static readonly Error DriverNotActive = Error.Validation(
        "Trips.Trip.DriverNotActive", "Only an active driver can be assigned to a trip.");

    public static readonly Error NotEditable = Error.Conflict(
        "Trips.Trip.NotEditable", "Only a scheduled trip can be edited.");

    public static readonly Error ManifestAlreadyAttached = Error.Conflict(
        "Trips.Trip.ManifestAlreadyAttached", "A different manifest is already attached to this trip.");

    public static Error InvalidStatusTransition(TripStatus from, TripStatus to) => Error.Conflict(
        "Trips.Trip.InvalidStatusTransition", $"A trip cannot move from {from} to {to}.");

    public static Error TerminalStatus(TripStatus status) => Error.Conflict(
        "Trips.Trip.TerminalStatus", $"A {status} trip can no longer be changed.");
}

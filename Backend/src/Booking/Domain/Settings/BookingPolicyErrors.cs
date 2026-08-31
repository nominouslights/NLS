using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.Settings;

/// <summary>Domain errors for the settings aggregates (policy + corridor settings).</summary>
public static class BookingPolicyErrors
{
    public static readonly Error NegativeValue = Error.Validation(
        "Booking.Policy.NegativeValue", "Policy hours, minimums, and penalties cannot be negative.");

    public static readonly Error InvalidSeatHold = Error.Validation(
        "Booking.Policy.InvalidSeatHold", "The seat hold must be at least 1 minute.");

    public static readonly Error InvalidSeatCapacity = Error.Validation(
        "Booking.Policy.InvalidSeatCapacity", "The default seat capacity must be at least 1.");

    public static readonly Error InvalidCorridorPassengerMinimum = Error.Validation(
        "Booking.CorridorSettings.InvalidPassengerMinimum", "A corridor passenger minimum cannot be negative.");

    public static readonly Error InvalidCorridorSeatCapacity = Error.Validation(
        "Booking.CorridorSettings.InvalidSeatCapacity", "A corridor seat capacity must be at least 1.");
}

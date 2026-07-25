using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Drivers;

/// <summary>All domain errors the Driver aggregate (and its handlers) can produce.</summary>
public static class DriverErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Drivers.Driver.NotFound", "The driver was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Drivers.Driver.NameRequired", "A driver name is required.");

    public static readonly Error LicenceClassRequired = Error.Validation(
        "Drivers.Driver.LicenceClassRequired", "A licence class is required.");

    public static readonly Error SourceRequired = Error.Validation(
        "Drivers.Driver.SourceRequired", "A driver source (Northern Link or the partner name) is required.");

    public static Error InvalidStatusTransition(DriverStatus from, DriverStatus to) => Error.Conflict(
        "Drivers.Driver.InvalidStatusTransition", $"A driver cannot move from {from} to {to}.");
}

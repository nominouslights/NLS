using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Clearances;

/// <summary>All domain errors the DriverClearance aggregate (and its handlers) can produce.</summary>
public static class ClearanceErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Drivers.Clearance.NotFound", "The clearance was not found.");

    public static readonly Error TitleRequired = Error.Validation(
        "Drivers.Clearance.TitleRequired", "A clearance title is required.");

    public static readonly Error ClientNameRequired = Error.Validation(
        "Drivers.Clearance.ClientNameRequired", "A client name is required.");
}

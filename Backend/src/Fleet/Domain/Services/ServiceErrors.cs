using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Services;

/// <summary>All domain errors the ServiceRecord aggregate (and its handlers) can produce.</summary>
public static class ServiceErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Fleet.Service.NotFound", "The service record was not found.");

    public static readonly Error PerformedByRequired = Error.Validation(
        "Fleet.Service.PerformedByRequired", "The technician or vendor who performed the service is required.");

    public static readonly Error ItemsRequired = Error.Validation(
        "Fleet.Service.ItemsRequired", "At least one item that was changed is required.");

    public static readonly Error ReasonRequired = Error.Validation(
        "Fleet.Service.ReasonRequired", "A reason for the service is required.");

    public static readonly Error InvalidOdometer = Error.Validation(
        "Fleet.Service.InvalidOdometer", "The odometer reading cannot be negative.");
}

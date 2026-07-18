using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>All domain errors the VehicleInspection aggregate (and its handlers) can produce.</summary>
public static class InspectionErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Fleet.Inspection.NotFound", "The inspection was not found.");

    public static readonly Error UnitRequired = Error.Validation(
        "Fleet.Inspection.UnitRequired", "A vehicle unit is required.");

    public static readonly Error DriverRequired = Error.Validation(
        "Fleet.Inspection.DriverRequired", "The driver who performed the inspection is required.");
}

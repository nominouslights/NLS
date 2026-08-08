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

    public static readonly Error WorkOrderAlreadyGenerated = Error.Conflict(
        "Fleet.Inspection.WorkOrderAlreadyGenerated",
        "A work order was already generated from this inspection.");

    /// <summary>
    /// A trip already has an inspection of this half (one pre-trip and one post-trip per trip is
    /// the invariant). The caller should edit the existing record rather than enter a second.
    /// </summary>
    public static Error DuplicateForTrip(InspectionType type) => Error.Conflict(
        "Fleet.Inspection.DuplicateForTrip",
        $"A {(type == InspectionType.PreTrip ? "pre" : "post")}-trip inspection already exists for this trip — edit it instead.");
}

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

    /// <summary>No defect on this inspection carries that item — the other half of the
    /// <c>(InspectionId, Item)</c> address.</summary>
    public static readonly Error DefectNotFound = Error.NotFound(
        "Fleet.Inspection.DefectNotFound", "No defect with that item was found on this inspection.");

    /// <summary>
    /// Resolution is final — there is no reopen, so a second resolve is always a mistake (in
    /// practice, a double-click). A fault that comes back is re-reported as a new defect on a
    /// later inspection instead.
    /// </summary>
    public static readonly Error DefectAlreadyResolved = Error.Conflict(
        "Fleet.Inspection.DefectAlreadyResolved", "That defect has already been resolved.");

    /// <summary>
    /// Two defects on one inspection share an item. <c>Item</c> is the addressing key for
    /// resolution, so a duplicate would make both rows resolve together — rejected on entry and
    /// on amendment rather than left to corrupt the maintenance record.
    /// </summary>
    public static readonly Error DuplicateDefectItem = Error.Validation(
        "Fleet.Inspection.DuplicateDefectItem",
        "Each defect on an inspection must name a different item.");

    /// <summary>
    /// A trip already has an inspection of this half (one pre-trip and one post-trip per trip is
    /// the invariant). The caller should edit the existing record rather than enter a second.
    /// </summary>
    public static Error DuplicateForTrip(InspectionType type) => Error.Conflict(
        "Fleet.Inspection.DuplicateForTrip",
        $"A {(type == InspectionType.PreTrip ? "pre" : "post")}-trip inspection already exists for this trip — edit it instead.");
}

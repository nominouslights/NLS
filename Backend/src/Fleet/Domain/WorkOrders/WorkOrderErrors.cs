using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.WorkOrders;

/// <summary>All domain errors the WorkOrder aggregate (and its handlers) can produce.</summary>
public static class WorkOrderErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Fleet.WorkOrder.NotFound", "The work order was not found.");

    public static readonly Error TitleRequired = Error.Validation(
        "Fleet.WorkOrder.TitleRequired", "A work order title is required.");

    public static readonly Error Terminal = Error.Conflict(
        "Fleet.WorkOrder.Terminal", "A completed or cancelled work order can no longer change.");

    public static readonly Error UseCompleteEndpoint = Error.Validation(
        "Fleet.WorkOrder.UseCompleteEndpoint", "Complete a work order through its completion action so the resolving service is recorded.");

    /// <summary>A defect line with no item cannot address a defect.</summary>
    public static readonly Error DefectItemRequired = Error.Validation(
        "Fleet.WorkOrder.DefectItemRequired", "Each defect on a work order must name its item.");

    /// <summary>The same (inspection, item) defect was listed twice on one work order.</summary>
    public static readonly Error DuplicateDefect = Error.Validation(
        "Fleet.WorkOrder.DuplicateDefect", "A defect can be listed only once on a work order.");

    /// <summary>A work order with defect lines was completed without an outcome for every line.</summary>
    public static readonly Error DefectOutcomeMissing = Error.Validation(
        "Fleet.WorkOrder.DefectOutcomeMissing",
        "Every defect on the work order needs an outcome — Repaired, NoFaultFound or Deferred.");

    /// <summary>An outcome was submitted for a defect that is not on this work order (or for a work order with no defect lines).</summary>
    public static readonly Error UnknownDefectOutcome = Error.Validation(
        "Fleet.WorkOrder.UnknownDefectOutcome", "An outcome was given for a defect that is not on this work order.");

    /// <summary>Two outcomes were submitted for the same defect line.</summary>
    public static readonly Error DuplicateDefectOutcome = Error.Validation(
        "Fleet.WorkOrder.DuplicateDefectOutcome", "Each defect on the work order takes exactly one outcome.");

    /// <summary>The outcome value is not a known <see cref="DefectRepairOutcome"/> (a raw out-of-range number).</summary>
    public static readonly Error InvalidDefectOutcome = Error.Validation(
        "Fleet.WorkOrder.InvalidDefectOutcome", "The defect outcome must be Repaired, NoFaultFound or Deferred.");

    /// <summary>Deferring leaves the defect open on the truck — the reason has to be written down.</summary>
    public static readonly Error DeferredNoteRequired = Error.Validation(
        "Fleet.WorkOrder.DeferredNoteRequired", "A deferred defect needs a note saying why it was not repaired.");

    /// <summary>An out-of-service defect must be repaired (or found not to exist) — never carried forward.</summary>
    public static readonly Error OutOfServiceCannotBeDeferred = Error.Validation(
        "Fleet.WorkOrder.OutOfServiceCannotBeDeferred", "An out-of-service defect cannot be deferred.");
}

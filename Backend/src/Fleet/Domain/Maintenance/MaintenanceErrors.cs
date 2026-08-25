using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// All domain errors the preventative-maintenance aggregates
/// (<see cref="MaintenancePlan"/>, <see cref="PlanAssignment"/>, <see cref="PmCompletion"/>)
/// and their handlers can produce.
/// </summary>
public static class MaintenanceErrors
{
    public static readonly Error PlanNotFound = Error.NotFound(
        "Fleet.MaintenancePlan.NotFound", "The maintenance plan was not found.");

    public static readonly Error NameTaken = Error.Conflict(
        "Fleet.MaintenancePlan.NameTaken", "A maintenance plan with this name already exists.");

    public static readonly Error NameRequired = Error.Validation(
        "Fleet.MaintenancePlan.NameRequired", "A maintenance plan name is required.");

    public static readonly Error VehicleModelRequired = Error.Validation(
        "Fleet.MaintenancePlan.VehicleModelRequired", "A maintenance plan needs the vehicle model it covers.");

    public static readonly Error ServiceClassRequired = Error.Validation(
        "Fleet.MaintenancePlan.ServiceClassRequired", "A maintenance plan needs a service class (e.g. severe).");

    public static readonly Error NameTooLong = Error.Validation(
        "Fleet.MaintenancePlan.NameTooLong", $"A maintenance plan name cannot exceed {MaintenancePlan.NameMaxLength} characters.");

    public static readonly Error VehicleModelTooLong = Error.Validation(
        "Fleet.MaintenancePlan.VehicleModelTooLong", $"A maintenance plan's vehicle model cannot exceed {MaintenancePlan.VehicleModelMaxLength} characters.");

    public static readonly Error ServiceClassTooLong = Error.Validation(
        "Fleet.MaintenancePlan.ServiceClassTooLong", $"A maintenance plan's service class cannot exceed {MaintenancePlan.ServiceClassMaxLength} characters.");

    public static readonly Error NotesTooLong = Error.Validation(
        "Fleet.MaintenancePlan.NotesTooLong", $"Maintenance plan notes cannot exceed {MaintenancePlan.NotesMaxLength} characters.");

    public static readonly Error ItemCodeRequired = Error.Validation(
        "Fleet.MaintenancePlan.ItemCodeRequired", "Every maintenance item needs a code.");

    public static readonly Error ItemCodeTooLong = Error.Validation(
        "Fleet.MaintenancePlan.ItemCodeTooLong", $"A maintenance item code cannot exceed {MaintenancePlan.CodeMaxLength} characters.");

    public static readonly Error DuplicateItemCode = Error.Validation(
        "Fleet.MaintenancePlan.DuplicateItemCode", "Maintenance item codes must be unique within a plan.");

    public static readonly Error ItemIntervalRequired = Error.Validation(
        "Fleet.MaintenancePlan.ItemIntervalRequired", "Every maintenance item needs at least one interval, and any interval given must be greater than zero.");

    public static readonly Error InvalidComponentTier = Error.Validation(
        "Fleet.MaintenancePlan.InvalidComponentTier", "A maintenance item's component tier is not a recognized value.");

    public static readonly Error InvalidMaintenanceTask = Error.Validation(
        "Fleet.MaintenancePlan.InvalidMaintenanceTask", "A maintenance item's task is not a recognized value.");

    public static readonly Error IntervalKmTooLarge = Error.Validation(
        "Fleet.MaintenancePlan.IntervalKmTooLarge", $"An interval in km cannot exceed {MaintenancePlan.MaxIntervalKm:N0} km.");

    public static readonly Error IntervalMonthsTooLarge = Error.Validation(
        "Fleet.MaintenancePlan.IntervalMonthsTooLarge", $"An interval in months cannot exceed {MaintenancePlan.MaxIntervalMonths} months.");

    public static readonly Error InvalidShopMinutes = Error.Validation(
        "Fleet.MaintenancePlan.InvalidShopMinutes", "Every maintenance item needs estimated shop minutes greater than zero.");

    public static readonly Error OverhaulCodeRequired = Error.Validation(
        "Fleet.MaintenancePlan.OverhaulCodeRequired", "Every overhaul needs a code.");

    public static readonly Error OverhaulCodeTooLong = Error.Validation(
        "Fleet.MaintenancePlan.OverhaulCodeTooLong", $"An overhaul code cannot exceed {MaintenancePlan.CodeMaxLength} characters.");

    public static readonly Error DuplicateOverhaulCode = Error.Validation(
        "Fleet.MaintenancePlan.DuplicateOverhaulCode", "Overhaul codes must be unique within a plan.");

    public static readonly Error OverhaulIntervalRequired = Error.Validation(
        "Fleet.MaintenancePlan.OverhaulIntervalRequired", "Every overhaul needs at least one interval, and any interval given must be greater than zero.");

    public static readonly Error InvalidLabourHours = Error.Validation(
        "Fleet.MaintenancePlan.InvalidLabourHours", "Every overhaul needs estimated labour hours greater than zero.");

    public static readonly Error UnknownRelatedItemCode = Error.Validation(
        "Fleet.MaintenancePlan.UnknownRelatedItemCode", "An overhaul's related item codes must reference maintenance items that exist in the plan.");

    public static readonly Error InvalidPartsCad = Error.Validation(
        "Fleet.MaintenancePlan.InvalidPartsCad", "An overhaul's parts estimate cannot be negative.");

    public static readonly Error InvalidLeadKm = Error.Validation(
        "Fleet.MaintenancePlan.InvalidLeadKm", "A due-soon lead override in km must be greater than zero.");

    public static readonly Error InvalidLeadDays = Error.Validation(
        "Fleet.MaintenancePlan.InvalidLeadDays", "A due-soon lead override in days must be greater than zero.");

    public static readonly Error LeadKmNotBelowInterval = Error.Validation(
        "Fleet.MaintenancePlan.LeadKmNotBelowInterval",
        "A due-soon lead in km must be smaller than the line's km interval — a lead at or past the interval would keep the line due-soon forever.");

    public static readonly Error LeadDaysNotBelowInterval = Error.Validation(
        "Fleet.MaintenancePlan.LeadDaysNotBelowInterval",
        "A due-soon lead in days must be smaller than the line's calendar interval (months × 28 days) — a lead at or past the interval would keep the line due-soon forever.");

    public static readonly Error ItemTierRequired = Error.Validation(
        "Fleet.MaintenancePlan.ItemTierRequired", "Every maintenance item needs a component tier.");

    public static readonly Error ItemTaskRequired = Error.Validation(
        "Fleet.MaintenancePlan.ItemTaskRequired", "Every maintenance item needs the task performed on it.");

    public static readonly Error AssignmentNotFound = Error.NotFound(
        "Fleet.PlanAssignment.NotFound", "The vehicle has no maintenance plan assigned.");

    public static readonly Error AssignmentConflict = Error.Conflict(
        "Fleet.PlanAssignment.Conflict", "The vehicle's plan assignment changed at the same time — retry the assignment.");

    public static readonly Error VehicleRequired = Error.Validation(
        "Fleet.PlanAssignment.VehicleRequired", "A plan assignment needs a vehicle.");

    public static readonly Error PlanRequired = Error.Validation(
        "Fleet.PlanAssignment.PlanRequired", "A plan assignment needs a maintenance plan.");

    public static readonly Error CompletionCodeRequired = Error.Validation(
        "Fleet.PmCompletion.CodeRequired", "A completion needs the maintenance item or overhaul code it certifies.");

    public static readonly Error CompletionCodeNotInPlan = Error.Validation(
        "Fleet.PmCompletion.CodeNotInPlan", "The code does not exist in the vehicle's assigned maintenance plan for that kind (item vs overhaul).");

    public static readonly Error CompletionCodeTooLong = Error.Validation(
        "Fleet.PmCompletion.CodeTooLong", $"A completion's item or overhaul code cannot exceed {MaintenancePlan.CodeMaxLength} characters.");

    public static readonly Error PerformedByRequired = Error.Validation(
        "Fleet.PmCompletion.PerformedByRequired", "A completion needs who performed the work.");

    public static readonly Error PerformedByTooLong = Error.Validation(
        "Fleet.PmCompletion.PerformedByTooLong", $"A completion's performed-by cannot exceed {PmCompletion.PerformedByMaxLength} characters.");

    public static readonly Error MeasurementTooLong = Error.Validation(
        "Fleet.PmCompletion.MeasurementTooLong", $"A completion's measurement cannot exceed {PmCompletion.MeasurementMaxLength} characters.");

    public static readonly Error CompletionNotesTooLong = Error.Validation(
        "Fleet.PmCompletion.NotesTooLong", $"Completion notes cannot exceed {PmCompletion.NotesMaxLength} characters.");

    public static readonly Error InvalidCompletionKind = Error.Validation(
        "Fleet.PmCompletion.InvalidKind", "A completion's kind must be Item or Overhaul.");

    public static readonly Error CompletionWorkOrderNotForVehicle = Error.Validation(
        "Fleet.PmCompletion.WorkOrderNotForVehicle",
        "The linked work order belongs to a different vehicle.");

    public static readonly Error InvalidOdometer = Error.Validation(
        "Fleet.PmCompletion.InvalidOdometer", "A completion's odometer reading cannot be negative.");

    public static readonly Error CompletionOdometerRequired = Error.Validation(
        "Fleet.PmCompletion.OdometerRequired", "A completion needs the odometer reading at the time of service.");

    public static Error OdometerImplausible(int vehicleOdometerKm) => Error.Validation(
        "Fleet.PmCompletion.OdometerImplausible",
        $"The completion's odometer reading is more than {PmCompletion.MaxOdometerAheadKm:N0} km ahead of the vehicle's current recorded odometer ({vehicleOdometerKm:N0} km) — check for a typo, or record the vehicle's odometer first.");

    public static readonly Error PerformedAtRequired = Error.Validation(
        "Fleet.PmCompletion.PerformedAtRequired", "A completion needs the date the work was performed.");

    public static readonly Error PerformedAtInFuture = Error.Validation(
        "Fleet.PmCompletion.PerformedAtInFuture", "A completion's performed date cannot be more than one day in the future.");
}

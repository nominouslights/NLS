using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Schedules;

/// <summary>All domain errors the ScheduleTemplate aggregate (and its handlers) can produce.</summary>
public static class ScheduleTemplateErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Trips.ScheduleTemplate.NotFound", "The schedule template was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Trips.ScheduleTemplate.NameRequired", "A template name is required.");

    public static readonly Error RouteRequired = Error.Validation(
        "Trips.ScheduleTemplate.RouteRequired", "A template must reference a route.");

    public static readonly Error AtLeastOneDay = Error.Validation(
        "Trips.ScheduleTemplate.AtLeastOneDay", "A template must run on at least one day of the week.");

    public static readonly Error InvalidInterval = Error.Validation(
        "Trips.ScheduleTemplate.InvalidInterval", "The recurrence interval must be between 1 and 365 days.");

    public static readonly Error AnchorRequired = Error.Validation(
        "Trips.ScheduleTemplate.AnchorRequired", "An every-N-days template must have an anchor date.");

    public static readonly Error AtLeastOneDayOfMonth = Error.Validation(
        "Trips.ScheduleTemplate.AtLeastOneDayOfMonth", "A monthly template must run on at least one day of the month.");

    public static readonly Error InvalidDayOfMonth = Error.Validation(
        "Trips.ScheduleTemplate.InvalidDayOfMonth", "Each day of the month must be between 1 and 31.");

    public static readonly Error InvalidSeats = Error.Validation(
        "Trips.ScheduleTemplate.InvalidSeats", "Seat capacity must be positive and the minimum cannot exceed it.");

    public static readonly Error InvalidHorizon = Error.Validation(
        "Trips.ScheduleTemplate.InvalidHorizon", "The generation horizon must be between 1 and 60 days.");

    public static readonly Error ReturnBeforeDeparture = Error.Validation(
        "Trips.ScheduleTemplate.ReturnBeforeDeparture", "The return departure must be after the outbound departure.");

    public static readonly Error ReturnNextDayRequiresReturnDeparture = Error.Validation(
        "Trips.ScheduleTemplate.ReturnNextDayRequiresReturnDeparture",
        "Return next day only applies when a return departure time is set.");

    // ---- On-demand generation (messages are shown verbatim in the Generate Trips dialog) ----

    public static readonly Error TemplateInactive = Error.Validation(
        "Trips.ScheduleTemplate.TemplateInactive",
        "This template is inactive, so it cannot generate trips. Activate it first.");

    public static readonly Error InvalidGenerationWindow = Error.Validation(
        "Trips.ScheduleTemplate.InvalidGenerationWindow",
        $"Choose a date from today up to {ScheduleTemplate.MaxGenerateAheadDays} days ahead.");

    public static readonly Error RouteMissing = Error.Validation(
        "Trips.ScheduleTemplate.RouteMissing",
        "This template's route no longer exists. Edit the template and choose a route.");

    public static readonly Error NoDefaultDriver = Error.Validation(
        "Trips.ScheduleTemplate.NoDefaultDriver",
        "This template has no default driver. A trip is never created unassigned — set one on the template first.");

    public static readonly Error DefaultDriverUnavailable = Error.Validation(
        "Trips.ScheduleTemplate.DefaultDriverUnavailable",
        "This template's default driver is no longer an active driver. Choose another driver on the template.");

    public static readonly Error NoDefaultVehicle = Error.Validation(
        "Trips.ScheduleTemplate.NoDefaultVehicle",
        "This template has no default vehicle unit. A trip is never created unassigned — set one on the template first.");

    public static readonly Error DefaultVehicleUnavailable = Error.Validation(
        "Trips.ScheduleTemplate.DefaultVehicleUnavailable",
        "This template's default vehicle unit is not an active fleet vehicle. Choose another unit on the template.");

    public static readonly Error GenerationConflict = Error.Conflict(
        "Trips.ScheduleTemplate.GenerationConflict",
        "Another generation for this template just ran — reopen the preview and try again.");

    // ---- Schedule exceptions (special dates) ----

    public static readonly Error ExceptionNotFound = Error.NotFound(
        "Trips.ScheduleTemplate.ExceptionNotFound", "The schedule exception was not found on this template.");

    public static readonly Error DuplicateExceptionDate = Error.Validation(
        "Trips.ScheduleTemplate.DuplicateExceptionDate", "The template already has an exception on that date.");

    public static readonly Error ExceptionTimesNotAllowed = Error.Validation(
        "Trips.ScheduleTemplate.ExceptionTimesNotAllowed", "A skip carries no times — the occurrence simply does not run.");

    public static readonly Error ExtraRunDepartureRequired = Error.Validation(
        "Trips.ScheduleTemplate.ExtraRunDepartureRequired", "An extra run needs a departure time of its own.");

    public static readonly Error ExceptionReturnBeforeDeparture = Error.Validation(
        "Trips.ScheduleTemplate.ExceptionReturnBeforeDeparture",
        "The exception's return departure must be after its outbound departure.");

    public static readonly Error OverrideTimeRequired = Error.Validation(
        "Trips.ScheduleTemplate.OverrideTimeRequired", "A time override must change at least one time.");

    public static readonly Error ExceptionReturnWithoutTemplateReturn = Error.Validation(
        "Trips.ScheduleTemplate.ExceptionReturnWithoutTemplateReturn",
        "A return-time override only applies to a template that has a return leg.");
}

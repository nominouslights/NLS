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
}

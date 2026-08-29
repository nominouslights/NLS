using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Routes;

/// <summary>All domain errors the Route aggregate (and its handlers) can produce.</summary>
public static class RouteErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Trips.Route.NotFound", "The route was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Trips.Route.NameRequired", "A route name is required.");

    public static readonly Error AtLeastTwoStops = Error.Validation(
        "Trips.Route.AtLeastTwoStops", "A route needs at least an origin and a destination stop.");

    public static readonly Error StopNameRequired = Error.Validation(
        "Trips.Route.StopNameRequired", "Every route stop needs a name.");

    public static readonly Error UnknownStop = Error.Validation(
        "Trips.Route.UnknownStop", "One or more selected stops do not exist.");

    public static readonly Error InactiveStop = Error.Validation(
        "Trips.Route.InactiveStop", "A route cannot include an inactive stop.");

    public static readonly Error InvalidDistance = Error.Validation(
        "Trips.Route.InvalidDistance", "The route distance must be greater than zero kilometres.");

    public static readonly Error InvalidDuration = Error.Validation(
        "Trips.Route.InvalidDuration", "The estimated duration must be greater than zero.");

    public static readonly Error PartialTimetable = Error.Validation(
        "Trips.Route.PartialTimetable",
        "A leg's timetable must cover every stop or none — some stops have a time and others do not.");

    public static readonly Error TimetableMustStartAtZero = Error.Validation(
        "Trips.Route.TimetableMustStartAtZero",
        "A leg's timetable must start at the leg's first stop, with an offset of zero minutes.");

    public static readonly Error TimetableNotIncreasing = Error.Validation(
        "Trips.Route.TimetableNotIncreasing",
        "A leg's timetable must increase at every stop along the direction of travel.");
}

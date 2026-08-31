namespace NorthernLink.Trips.Application.Schedules;

/// <summary>
/// Public contract for one schedule exception (special date). <see cref="Date"/> travels
/// as "yyyy-MM-dd" and <see cref="Kind"/> as the enum name ("Skip" | "ExtraRun" |
/// "TimeOverride"). The time fields follow the kind's rules: both null for Skip; for
/// ExtraRun the departure is always set and the return means a same-day return leg; for
/// TimeOverride each non-null value replaces the template's own time.
/// </summary>
public sealed record ScheduleExceptionResponse(
    Guid Id,
    Guid ScheduleTemplateId,
    string Date,
    string Kind,
    TimeOnly? DepartureTime,
    TimeOnly? ReturnDepartureTime,
    string? Note,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

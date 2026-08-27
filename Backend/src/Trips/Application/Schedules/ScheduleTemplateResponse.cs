namespace NorthernLink.Trips.Application.Schedules;

/// <summary>
/// Public contract for a schedule template. Days, the recurrence kind, and the service
/// type travel as string names ("Monday", "MonthlyDays", "ContractCrew"). Only the fields
/// for <see cref="RecurrenceKind"/> are populated: DaysOfWeek → <see cref="DaysOfWeek"/>;
/// EveryNDays → <see cref="IntervalDays"/> + <see cref="AnchorDate"/> ("yyyy-MM-dd");
/// MonthlyDays → <see cref="DaysOfMonth"/>. <see cref="RouteName"/> is resolved by the
/// projection from the routes table for display convenience.
/// </summary>
public sealed record ScheduleTemplateResponse(
    Guid Id,
    string Name,
    Guid RouteId,
    string? RouteName,
    string ServiceType,
    Guid? ClientId,
    string? ClientName,
    string RecurrenceKind,
    IReadOnlyList<string> DaysOfWeek,
    int? IntervalDays,
    string? AnchorDate,
    IReadOnlyList<int> DaysOfMonth,
    TimeOnly DepartureTime,
    TimeOnly? ReturnDepartureTime,
    bool ReturnNextDay,
    int SeatsCapacity,
    int? SeatsMinimum,
    string? DefaultVehicleUnit,
    Guid? DefaultDriverId,
    int GenerationHorizonDays,
    string? CutoffNote,
    bool Active,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

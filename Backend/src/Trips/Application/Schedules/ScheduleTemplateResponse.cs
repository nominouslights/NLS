namespace NorthernLink.Trips.Application.Schedules;

/// <summary>
/// Public contract for a schedule template. Days and the service type travel as string
/// names ("Monday", "ContractCrew"); the weekly grid is derived client-side.
/// <see cref="RouteName"/> is resolved by the projection from the routes table for
/// display convenience.
/// </summary>
public sealed record ScheduleTemplateResponse(
    Guid Id,
    string Name,
    Guid RouteId,
    string? RouteName,
    string ServiceType,
    Guid? ClientId,
    string? ClientName,
    IReadOnlyList<string> DaysOfWeek,
    TimeOnly DepartureTime,
    TimeOnly? ReturnDepartureTime,
    int SeatsCapacity,
    int? SeatsMinimum,
    string? DefaultVehicleUnit,
    Guid? DefaultDriverId,
    int GenerationHorizonDays,
    string? CutoffNote,
    bool Active,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

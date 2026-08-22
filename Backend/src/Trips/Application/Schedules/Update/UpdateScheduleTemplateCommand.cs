using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Schedules.Update;

/// <summary>
/// Full-row template edit. Only affects trips not yet generated — already-materialized
/// trips keep their snapshots. Activation moves through its own commands.
/// </summary>
public sealed record UpdateScheduleTemplateCommand(
    Guid TemplateId,
    string Name,
    Guid RouteId,
    TripServiceType ServiceType,
    Guid? ClientId,
    string? ClientName,
    ScheduleRecurrenceKind RecurrenceKind,
    IReadOnlyList<DayOfWeek> DaysOfWeek,
    int? IntervalDays,
    DateOnly? AnchorDate,
    IReadOnlyList<int> DaysOfMonth,
    TimeOnly DepartureTime,
    TimeOnly? ReturnDepartureTime,
    int SeatsCapacity,
    int? SeatsMinimum,
    string? DefaultVehicleUnit,
    Guid? DefaultDriverId,
    int GenerationHorizonDays,
    string? CutoffNote) : ICommand;

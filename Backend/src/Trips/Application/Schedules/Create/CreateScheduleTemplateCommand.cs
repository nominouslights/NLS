using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Schedules.Create;

/// <summary>
/// Creates a weekly schedule template (active by default — the generation worker picks
/// it up on its next pass). A supplied <see cref="ClientId"/> is resolved against
/// client_lookup for the name snapshot, falling back to <see cref="ClientName"/>.
/// </summary>
public sealed record CreateScheduleTemplateCommand(
    Guid TenantId,
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
    int GenerationHorizonDays = ScheduleTemplate.DefaultHorizonDays,
    string? CutoffNote = null) : ICommand<Guid>;

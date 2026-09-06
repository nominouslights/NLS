using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Application.Schedules.AddException;

/// <summary>
/// Adds a dated exception (skip / extra run / time override) to a schedule template.
/// One exception per date; per-kind time rules live on the aggregate. Returns the new
/// exception's id.
/// </summary>
public sealed record AddScheduleExceptionCommand(
    Guid TemplateId,
    DateOnly Date,
    ScheduleExceptionKind Kind,
    TimeOnly? DepartureTime,
    TimeOnly? ReturnDepartureTime,
    string? Note) : ICommand<Guid>;

using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Application.Schedules.UpdateException;

/// <summary>Full-row edit of one schedule exception — the same per-kind rules as adding one.</summary>
public sealed record UpdateScheduleExceptionCommand(
    Guid TemplateId,
    Guid ExceptionId,
    DateOnly Date,
    ScheduleExceptionKind Kind,
    TimeOnly? DepartureTime,
    TimeOnly? ReturnDepartureTime,
    string? Note) : ICommand;

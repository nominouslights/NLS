using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Schedules.RemoveException;

/// <summary>
/// Deletes one schedule exception — the date reverts to the template's plain recurrence
/// on the next generation pass. Trips already materialized are untouched.
/// </summary>
public sealed record RemoveScheduleExceptionCommand(Guid TemplateId, Guid ExceptionId) : ICommand;

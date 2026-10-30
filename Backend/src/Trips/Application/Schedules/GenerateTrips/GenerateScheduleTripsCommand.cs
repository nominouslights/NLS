using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Schedules.GenerateTrips;

/// <summary>
/// Materializes a template's trips from today through <see cref="Through"/> (inclusive, at
/// most <c>ScheduleTemplate.MaxGenerateAheadDays</c> ahead) on a dispatcher's demand, so
/// future months exist for the client accruals report. Idempotent: occurrences already
/// generated (by the worker or an earlier run) are skipped and counted as
/// <c>AlreadyExisted</c>. The template's later edits and special dates never reach trips
/// created here — cancel or edit those from Trips.
/// </summary>
public sealed record GenerateScheduleTripsCommand(Guid TenantId, Guid TemplateId, DateOnly Through)
    : ICommand<ScheduleTripGenerationResult>;

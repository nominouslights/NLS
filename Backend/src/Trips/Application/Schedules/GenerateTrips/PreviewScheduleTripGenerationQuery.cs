using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Schedules.GenerateTrips;

/// <summary>
/// What <see cref="GenerateScheduleTripsCommand"/> would do for the same window — the
/// same guards, the same counts, nothing persisted. A query rather than a dry-run flag on
/// the command: it never mutates, and <c>ISender.Query</c> is the existing read seam.
/// </summary>
public sealed record PreviewScheduleTripGenerationQuery(Guid TenantId, Guid TemplateId, DateOnly Through)
    : IQuery<ScheduleTripGenerationResult>;

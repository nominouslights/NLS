using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Schedules.GetScheduleTemplates;

/// <summary>All of the tenant's schedule templates (the weekly grid is derived client-side).</summary>
public sealed record GetScheduleTemplatesQuery(Guid TenantId)
    : IQuery<IReadOnlyList<ScheduleTemplateResponse>>;

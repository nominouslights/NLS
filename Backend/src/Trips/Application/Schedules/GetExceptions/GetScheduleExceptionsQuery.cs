using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Schedules.GetExceptions;

/// <summary>One template's exceptions (special dates), ascending by date. A template with none returns an empty list.</summary>
public sealed record GetScheduleExceptionsQuery(Guid TenantId, Guid TemplateId)
    : IQuery<IReadOnlyList<ScheduleExceptionResponse>>;

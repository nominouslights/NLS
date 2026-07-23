using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Schedules.GetScheduleTemplates;

public sealed class GetScheduleTemplatesQueryHandler(IScheduleTemplateReadService readService)
    : IQueryHandler<GetScheduleTemplatesQuery, IReadOnlyList<ScheduleTemplateResponse>>
{
    public async Task<Result<IReadOnlyList<ScheduleTemplateResponse>>> Handle(
        GetScheduleTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        var templates = await readService.GetTemplatesAsync(cancellationToken);
        return Result.Success(templates);
    }
}

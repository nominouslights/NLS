using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Schedules.GetExceptions;

public sealed class GetScheduleExceptionsQueryHandler(IScheduleTemplateReadService readService)
    : IQueryHandler<GetScheduleExceptionsQuery, IReadOnlyList<ScheduleExceptionResponse>>
{
    public async Task<Result<IReadOnlyList<ScheduleExceptionResponse>>> Handle(
        GetScheduleExceptionsQuery query,
        CancellationToken cancellationToken)
    {
        var exceptions = await readService.GetExceptionsForTemplateAsync(query.TemplateId, cancellationToken);
        return Result.Success(exceptions);
    }
}

using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.GetAll;

public sealed class GetMaintenancePlansQueryHandler(IPmReadService readService)
    : IQueryHandler<GetMaintenancePlansQuery, IReadOnlyList<MaintenancePlanSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<MaintenancePlanSummaryResponse>>> Handle(
        GetMaintenancePlansQuery query,
        CancellationToken cancellationToken)
    {
        var plans = await readService.GetPlansAsync(cancellationToken);
        return Result.Success(plans);
    }
}

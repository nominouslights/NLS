using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.GetById;

public sealed class GetMaintenancePlanByIdQueryHandler(IPmReadService readService)
    : IQueryHandler<GetMaintenancePlanByIdQuery, MaintenancePlanResponse>
{
    public async Task<Result<MaintenancePlanResponse>> Handle(
        GetMaintenancePlanByIdQuery query,
        CancellationToken cancellationToken)
    {
        var plan = await readService.GetPlanByIdAsync(query.PlanId, cancellationToken);
        return plan is null
            ? Result.Failure<MaintenancePlanResponse>(MaintenanceErrors.PlanNotFound)
            : Result.Success(plan);
    }
}

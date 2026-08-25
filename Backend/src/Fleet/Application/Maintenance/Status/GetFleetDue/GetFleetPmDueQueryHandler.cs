using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetFleetDue;

public sealed class GetFleetPmDueQueryHandler(IPmReadService readService)
    : IQueryHandler<GetFleetPmDueQuery, FleetPmDueResponse>
{
    public async Task<Result<FleetPmDueResponse>> Handle(
        GetFleetPmDueQuery query,
        CancellationToken cancellationToken)
    {
        var due = await readService.GetFleetDueAsync(cancellationToken);
        return Result.Success(due);
    }
}

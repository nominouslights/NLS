using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetDue;

public sealed class GetPmDueQueryHandler(IPmReadService readService)
    : IQueryHandler<GetPmDueQuery, PmDueResponse>
{
    public async Task<Result<PmDueResponse>> Handle(GetPmDueQuery query, CancellationToken cancellationToken)
    {
        var due = await readService.GetDueAsync(query.VehicleId, cancellationToken);
        return due is null
            ? Result.Failure<PmDueResponse>(VehicleErrors.NotFound)
            : Result.Success(due);
    }
}

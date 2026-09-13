using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.GetDefects;

public sealed class GetVehicleDefectsQueryHandler(IVehicleDefectReadService readService)
    : IQueryHandler<GetVehicleDefectsQuery, IReadOnlyList<VehicleDefectResponse>>
{
    public async Task<Result<IReadOnlyList<VehicleDefectResponse>>> Handle(
        GetVehicleDefectsQuery query,
        CancellationToken cancellationToken)
    {
        var defects = await readService.GetDefectsForVehicleAsync(
            query.VehicleId, query.IncludeResolved, cancellationToken);

        return Result.Success(defects);
    }
}

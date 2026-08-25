using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetVehicleStatus;

public sealed class GetVehiclePmStatusQueryHandler(IPmReadService readService)
    : IQueryHandler<GetVehiclePmStatusQuery, VehiclePmStatusResponse>
{
    public async Task<Result<VehiclePmStatusResponse>> Handle(
        GetVehiclePmStatusQuery query,
        CancellationToken cancellationToken)
    {
        var status = await readService.GetVehicleStatusAsync(query.VehicleId, cancellationToken);
        return status is null
            ? Result.Failure<VehiclePmStatusResponse>(VehicleErrors.NotFound)
            : Result.Success(status);
    }
}

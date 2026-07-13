using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Application.Vehicles.GetVehicles;

public sealed class GetVehiclesQueryHandler(IVehicleReadService readService)
    : IQueryHandler<GetVehiclesQuery, IReadOnlyList<VehicleResponse>>
{
    public async Task<Result<IReadOnlyList<VehicleResponse>>> Handle(
        GetVehiclesQuery query,
        CancellationToken cancellationToken)
    {
        var vehicles = await readService.GetVehiclesAsync(cancellationToken);
        return Result.Success(vehicles);
    }
}

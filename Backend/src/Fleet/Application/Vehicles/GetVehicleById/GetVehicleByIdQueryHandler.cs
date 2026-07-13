using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Application.Vehicles.GetVehicleById;

public sealed class GetVehicleByIdQueryHandler(IVehicleReadService readService)
    : IQueryHandler<GetVehicleByIdQuery, VehicleResponse>
{
    public async Task<Result<VehicleResponse>> Handle(
        GetVehicleByIdQuery query,
        CancellationToken cancellationToken)
    {
        var vehicle = await readService.GetVehicleAsync(query.VehicleId, cancellationToken);

        return vehicle is null
            ? Result.Failure<VehicleResponse>(VehicleErrors.NotFound)
            : Result.Success(vehicle);
    }
}

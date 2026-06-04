using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

internal sealed class RestoreVehicleCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<RestoreVehicleCommand>
{
    public async Task Handle(RestoreVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetDeletedByIdAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Archived vehicle {request.VehicleId} not found.");

        vehicle.Restore();

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}

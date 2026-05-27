using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

internal sealed class UpdateVehicleOdometerCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<UpdateVehicleOdometerCommand>
{
    public async Task Handle(UpdateVehicleOdometerCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.Id} not found.");

        vehicle.UpdateOdometer(request.NewOdometerKm);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}

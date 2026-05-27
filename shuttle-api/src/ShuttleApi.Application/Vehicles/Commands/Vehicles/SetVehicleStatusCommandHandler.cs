using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

internal sealed class SetVehicleStatusCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<SetVehicleStatusCommand>
{
    public async Task Handle(SetVehicleStatusCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.Id} not found.");

        vehicle.SetStatus(request.Status, request.StatusNote);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}

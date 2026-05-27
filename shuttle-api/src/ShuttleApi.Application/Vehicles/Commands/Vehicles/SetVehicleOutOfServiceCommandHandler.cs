using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

internal sealed class SetVehicleOutOfServiceCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<SetVehicleOutOfServiceCommand>
{
    public async Task Handle(SetVehicleOutOfServiceCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.Id} not found.");

        // Domain method enforces non-empty reason via Guard
        vehicle.SetOutOfService(request.Reason);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}

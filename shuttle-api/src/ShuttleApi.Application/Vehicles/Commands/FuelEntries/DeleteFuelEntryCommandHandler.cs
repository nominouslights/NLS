using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.FuelEntries;

internal sealed class DeleteFuelEntryCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<DeleteFuelEntryCommand>
{
    public async Task Handle(DeleteFuelEntryCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdWithRecordsAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        vehicle.RemoveFuelEntry(request.EntryId);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}

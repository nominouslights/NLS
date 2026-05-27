using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.ServiceRecords;

internal sealed class DeleteServiceRecordCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<DeleteServiceRecordCommand>
{
    public async Task Handle(DeleteServiceRecordCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdWithRecordsAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        var record = vehicle.ServiceRecords.FirstOrDefault(r => r.Id == request.RecordId)
            ?? throw new NotFoundException($"Service record {request.RecordId} not found.");

        vehicle.RemoveServiceRecord(record.Id);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}

using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.InspectionRecords;

internal sealed class DeleteInspectionRecordCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<DeleteInspectionRecordCommand>
{
    public async Task Handle(DeleteInspectionRecordCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdWithRecordsAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        var record = vehicle.InspectionRecords.FirstOrDefault(r => r.Id == request.RecordId)
            ?? throw new NotFoundException($"Inspection record {request.RecordId} not found.");

        vehicle.RemoveInspectionRecord(record.Id);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}

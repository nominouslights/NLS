using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.ServiceRecords;

internal sealed class CompleteServiceRecordCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<CompleteServiceRecordCommand>
{
    public async Task Handle(CompleteServiceRecordCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdWithRecordsAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        var record = vehicle.ServiceRecords.FirstOrDefault(r => r.Id == request.RecordId)
            ?? throw new NotFoundException($"Service record {request.RecordId} not found.");

        record.Complete(
            DateTime.SpecifyKind(request.CompletedDate, DateTimeKind.Utc),
            request.ActualCostDollars,
            request.OdometerAtService);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}

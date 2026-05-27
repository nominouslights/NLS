using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.ServiceRecords;

internal sealed class UpdateServiceRecordCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<UpdateServiceRecordCommand>
{
    public async Task Handle(UpdateServiceRecordCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdWithRecordsAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        var record = vehicle.ServiceRecords.FirstOrDefault(r => r.Id == request.RecordId)
            ?? throw new NotFoundException($"Service record {request.RecordId} not found.");

        record.Update(
            request.ServiceCategory,
            request.FluidType,
            request.Title,
            request.Description,
            request.IsPlanned,
            request.ServiceStatus,
            request.Priority,
            request.ScheduledDate.HasValue
                ? DateTime.SpecifyKind(request.ScheduledDate.Value, DateTimeKind.Utc)
                : null,
            request.OdometerAtService,
            request.EstimatedCostDollars,
            request.ServiceProvider,
            request.TechnicianName,
            request.PartsNotes,
            request.IsWarrantyWork,
            request.NextServiceDueDateUtc.HasValue
                ? DateTime.SpecifyKind(request.NextServiceDueDateUtc.Value, DateTimeKind.Utc)
                : null,
            request.NextServiceDueOdometerKm);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}

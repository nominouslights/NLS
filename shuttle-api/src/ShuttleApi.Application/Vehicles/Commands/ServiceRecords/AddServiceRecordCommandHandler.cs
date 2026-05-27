using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.ServiceRecords;

internal sealed class AddServiceRecordCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<AddServiceRecordCommand, AddServiceRecordResult>
{
    public async Task<AddServiceRecordResult> Handle(AddServiceRecordCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        var record = VehicleServiceRecord.Create(
            request.VehicleId,
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

        vehicle.AddServiceRecord(record);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);

        return new AddServiceRecordResult(record.Id);
    }
}

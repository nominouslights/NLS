using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.InspectionRecords;

internal sealed class UpdateInspectionRecordCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<UpdateInspectionRecordCommand>
{
    public async Task Handle(UpdateInspectionRecordCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdWithRecordsAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        var record = vehicle.InspectionRecords.FirstOrDefault(r => r.Id == request.RecordId)
            ?? throw new NotFoundException($"Inspection record {request.RecordId} not found.");

        record.Update(
            request.InspectionType,
            DateTime.SpecifyKind(request.InspectedAt, DateTimeKind.Utc),
            request.ExpiresAt.HasValue
                ? DateTime.SpecifyKind(request.ExpiresAt.Value, DateTimeKind.Utc)
                : null,
            request.InspectorName,
            request.InspectionFacility,
            request.CertificateNumber,
            request.InspectionResult,
            request.DeficienciesNotes,
            request.CorrectiveActionNotes,
            request.CostDollars);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
    }
}

using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.InspectionRecords;

internal sealed class AddInspectionRecordCommandHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<AddInspectionRecordCommand, AddInspectionRecordResult>
{
    public async Task<AddInspectionRecordResult> Handle(AddInspectionRecordCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        var record = VehicleInspectionRecord.Create(
            request.VehicleId,
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

        vehicle.AddInspectionRecord(record);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);

        return new AddInspectionRecordResult(record.Id);
    }
}

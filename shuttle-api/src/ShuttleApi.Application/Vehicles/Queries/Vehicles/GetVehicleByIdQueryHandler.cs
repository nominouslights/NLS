using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Queries.Vehicles;

internal sealed class GetVehicleByIdQueryHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<GetVehicleByIdQuery, VehicleDetailResult>
{
    public async Task<VehicleDetailResult> Handle(
        GetVehicleByIdQuery request,
        CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdWithRecordsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.Id} not found.");

        return new VehicleDetailResult(
            vehicle.Id,
            vehicle.UnitCode,
            vehicle.VIN,
            vehicle.Make,
            vehicle.Model,
            vehicle.Year,
            vehicle.Color,
            vehicle.LicensePlate,
            vehicle.Province,
            vehicle.VehicleType.ToString(),
            vehicle.PassengerCapacity,
            vehicle.CurrentOdometerKm,
            vehicle.AcquisitionDate,
            vehicle.RegistrationExpiry,
            vehicle.InsuranceProvider,
            vehicle.InsurancePolicyNumber,
            vehicle.InsuranceExpiry,
            vehicle.Status.ToString(),
            vehicle.StatusNote,
            vehicle.IsActive,
            vehicle.CreatedAt,
            vehicle.Notes,
            VehicleReadiness.ComputeScore(vehicle),
            VehicleReadiness.GetAlerts(vehicle),
            vehicle.IsRegistrationExpiringSoon,
            vehicle.IsInsuranceExpiringSoon,
            vehicle.ServiceRecords.Select(r => new ServiceRecordResult(
                r.Id,
                r.ServiceCategory.ToString(),
                r.FluidType?.ToString(),
                r.Title,
                r.Description,
                r.IsPlanned,
                r.ServiceStatus.ToString(),
                r.Priority.ToString(),
                r.ScheduledDate,
                r.StartedDate,
                r.CompletedDate,
                r.OdometerAtService,
                r.EstimatedCostDollars,
                r.ActualCostDollars,
                r.ServiceProvider,
                r.TechnicianName,
                r.PartsNotes,
                r.IsWarrantyWork,
                r.NextServiceDueDateUtc,
                r.NextServiceDueOdometerKm,
                r.CreatedAt)).ToList(),
            vehicle.InspectionRecords.Select(i => new InspectionRecordResult(
                i.Id,
                i.InspectionType.ToString(),
                i.InspectedAt,
                i.ExpiresAt,
                i.InspectorName,
                i.InspectionFacility,
                i.CertificateNumber,
                i.InspectionResult.ToString(),
                i.DeficienciesNotes,
                i.CorrectiveActionNotes,
                i.CostDollars,
                i.CreatedAt,
                i.IsExpiringSoon)).ToList());
    }
}

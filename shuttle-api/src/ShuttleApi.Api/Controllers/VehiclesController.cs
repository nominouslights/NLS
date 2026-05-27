using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Vehicles.Commands.InspectionRecords;
using ShuttleApi.Application.Vehicles.Commands.ServiceRecords;
using ShuttleApi.Application.Vehicles.Commands.Vehicles;
using ShuttleApi.Application.Vehicles.Queries.Vehicles;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Api.Controllers;

[Authorize]
public sealed class VehiclesController(ISender sender) : BaseApiController(sender)
{
    // ── Vehicles CRUD ─────────────────────────────────────────────────────────

    [HttpGet]
    [Route("api/vehicles")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetVehiclesQuery(), cancellationToken));

    [HttpGet]
    [Route("api/vehicles/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetVehicleByIdQuery(id), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/vehicles")]
    public async Task<IActionResult> Create([FromBody] CreateVehicleCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/vehicles/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateVehicleCommand(
            id,
            request.UnitCode,
            request.VIN,
            request.Make,
            request.Model,
            request.Year,
            request.Color,
            request.LicensePlate,
            request.Province,
            request.VehicleType,
            request.PassengerCapacity,
            request.CurrentOdometerKm,
            request.AcquisitionDate,
            request.RegistrationExpiry,
            request.InsuranceProvider,
            request.InsurancePolicyNumber,
            request.InsuranceExpiry,
            request.IsActive,
            request.Notes), cancellationToken);

        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/vehicles/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteVehicleCommand(id), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch]
    [Route("api/vehicles/{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetVehicleStatusRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new SetVehicleStatusCommand(id, request.Status, request.StatusNote), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/vehicles/{id:guid}/out-of-service")]
    public async Task<IActionResult> SetOutOfService(Guid id, [FromBody] SetOutOfServiceRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new SetVehicleOutOfServiceCommand(id, request.Reason), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch]
    [Route("api/vehicles/{id:guid}/odometer")]
    public async Task<IActionResult> UpdateOdometer(Guid id, [FromBody] UpdateOdometerRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateVehicleOdometerCommand(id, request.NewOdometerKm), cancellationToken);
        return NoContent();
    }

    // ── Service Records ───────────────────────────────────────────────────────

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/vehicles/{id:guid}/service-records")]
    public async Task<IActionResult> AddServiceRecord(Guid id, [FromBody] AddServiceRecordRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new AddServiceRecordCommand(
            id,
            request.ServiceCategory,
            request.FluidType,
            request.Title,
            request.Description,
            request.IsPlanned,
            request.ServiceStatus,
            request.Priority,
            request.ScheduledDate,
            request.OdometerAtService,
            request.EstimatedCostDollars,
            request.ServiceProvider,
            request.TechnicianName,
            request.PartsNotes,
            request.IsWarrantyWork,
            request.NextServiceDueDateUtc,
            request.NextServiceDueOdometerKm), cancellationToken);

        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/vehicles/{id:guid}/service-records/{recordId:guid}")]
    public async Task<IActionResult> UpdateServiceRecord(Guid id, Guid recordId, [FromBody] AddServiceRecordRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateServiceRecordCommand(
            id,
            recordId,
            request.ServiceCategory,
            request.FluidType,
            request.Title,
            request.Description,
            request.IsPlanned,
            request.ServiceStatus,
            request.Priority,
            request.ScheduledDate,
            request.OdometerAtService,
            request.EstimatedCostDollars,
            request.ServiceProvider,
            request.TechnicianName,
            request.PartsNotes,
            request.IsWarrantyWork,
            request.NextServiceDueDateUtc,
            request.NextServiceDueOdometerKm), cancellationToken);

        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/vehicles/{id:guid}/service-records/{recordId:guid}/complete")]
    public async Task<IActionResult> CompleteServiceRecord(Guid id, Guid recordId, [FromBody] CompleteServiceRecordRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new CompleteServiceRecordCommand(id, recordId, request.CompletedDate, request.ActualCostDollars, request.OdometerAtService), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/vehicles/{id:guid}/service-records/{recordId:guid}")]
    public async Task<IActionResult> DeleteServiceRecord(Guid id, Guid recordId, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteServiceRecordCommand(id, recordId), cancellationToken);
        return NoContent();
    }

    // ── Inspection Records ────────────────────────────────────────────────────

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/vehicles/{id:guid}/inspection-records")]
    public async Task<IActionResult> AddInspectionRecord(Guid id, [FromBody] AddInspectionRecordRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new AddInspectionRecordCommand(
            id,
            request.InspectionType,
            request.InspectedAt,
            request.ExpiresAt,
            request.InspectorName,
            request.InspectionFacility,
            request.CertificateNumber,
            request.InspectionResult,
            request.DeficienciesNotes,
            request.CorrectiveActionNotes,
            request.CostDollars), cancellationToken);

        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/vehicles/{id:guid}/inspection-records/{recordId:guid}")]
    public async Task<IActionResult> UpdateInspectionRecord(Guid id, Guid recordId, [FromBody] AddInspectionRecordRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateInspectionRecordCommand(
            id,
            recordId,
            request.InspectionType,
            request.InspectedAt,
            request.ExpiresAt,
            request.InspectorName,
            request.InspectionFacility,
            request.CertificateNumber,
            request.InspectionResult,
            request.DeficienciesNotes,
            request.CorrectiveActionNotes,
            request.CostDollars), cancellationToken);

        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/vehicles/{id:guid}/inspection-records/{recordId:guid}")]
    public async Task<IActionResult> DeleteInspectionRecord(Guid id, Guid recordId, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteInspectionRecordCommand(id, recordId), cancellationToken);
        return NoContent();
    }
}

// ── Inline request records ────────────────────────────────────────────────────

public sealed record UpdateVehicleRequest(
    string UnitCode,
    string VIN,
    string Make,
    string Model,
    int Year,
    string Color,
    string LicensePlate,
    string Province,
    VehicleType VehicleType,
    int PassengerCapacity,
    int CurrentOdometerKm,
    DateTime AcquisitionDate,
    DateTime? RegistrationExpiry,
    string? InsuranceProvider,
    string? InsurancePolicyNumber,
    DateTime? InsuranceExpiry,
    bool IsActive,
    string? Notes);

public sealed record SetVehicleStatusRequest(VehicleStatus Status, string? StatusNote);

public sealed record SetOutOfServiceRequest(string Reason);

public sealed record UpdateOdometerRequest(int NewOdometerKm);

public sealed record AddServiceRecordRequest(
    ServiceCategory ServiceCategory,
    FluidType? FluidType,
    string Title,
    string? Description,
    bool IsPlanned,
    ServiceStatus ServiceStatus,
    ServicePriority Priority,
    DateTime? ScheduledDate,
    int? OdometerAtService,
    decimal? EstimatedCostDollars,
    string? ServiceProvider,
    string? TechnicianName,
    string? PartsNotes,
    bool IsWarrantyWork,
    DateTime? NextServiceDueDateUtc,
    int? NextServiceDueOdometerKm);

public sealed record CompleteServiceRecordRequest(
    DateTime CompletedDate,
    decimal? ActualCostDollars,
    int? OdometerAtService);

public sealed record AddInspectionRecordRequest(
    InspectionType InspectionType,
    DateTime InspectedAt,
    DateTime? ExpiresAt,
    string? InspectorName,
    string? InspectionFacility,
    string? CertificateNumber,
    InspectionResult InspectionResult,
    string? DeficienciesNotes,
    string? CorrectiveActionNotes,
    decimal? CostDollars);

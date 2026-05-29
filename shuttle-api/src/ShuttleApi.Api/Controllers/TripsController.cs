using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Trips;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Api.Controllers;

[Authorize]
public sealed class TripsController(ISender sender) : BaseApiController(sender)
{
    [HttpGet]
    [Route("api/trips")]
    public async Task<IActionResult> GetAll(
        [FromQuery] TripStatus? status,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? driverId,
        [FromQuery] Guid? vehicleId,
        [FromQuery] TripServiceType? serviceType,
        CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetTripsQuery(status, clientId, driverId, vehicleId, serviceType), cancellationToken));

    [HttpGet]
    [Route("api/trips/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetTripByIdQuery(id), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/trips")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTripRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CreateTripCommand(
            request.ServiceType,
            request.ClientId,
            request.VehicleId,
            request.PurchaseOrderNumber,
            request.VehicleType,
            request.ScheduledAt,
            request.Notes,
            request.Stops.Select(s => new StopDto(s.SequenceOrder, s.LocationName, s.Address)).ToList(),
            request.SeatCapacity,
            request.PricePerSeat),
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/trips/{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTripRequest request,
        CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateTripCommand(
            id,
            request.VehicleId,
            request.PurchaseOrderNumber,
            request.VehicleType,
            request.ScheduledAt,
            request.Notes,
            request.Stops.Select(s => new StopDto(s.SequenceOrder, s.LocationName, s.Address)).ToList(),
            request.SeatCapacity,
            request.PricePerSeat),
            cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/trips/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteTripCommand(id), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/trips/{id:guid}/assign-driver")]
    public async Task<IActionResult> AssignDriver(
        Guid id,
        [FromBody] AssignDriverRequest request,
        CancellationToken cancellationToken)
    {
        await Sender.Send(new AssignDriverCommand(id, request.DriverId, request.VehicleType), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/trips/{id:guid}/dispatch")]
    public async Task<IActionResult> Dispatch(Guid id, CancellationToken cancellationToken)
    {
        await Sender.Send(new DispatchTripCommand(id), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "DriverOrAdmin")]
    [HttpPut]
    [Route("api/trips/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateTripStatusCommand(id, request.Status), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "DriverOrAdmin")]
    [HttpPost]
    [Route("api/trips/{id:guid}/pre-inspection")]
    public async Task<IActionResult> SubmitPreInspection(
        Guid id,
        [FromBody] SubmitPreInspectionRequest request,
        CancellationToken cancellationToken)
    {
        await Sender.Send(new SubmitPreInspectionCommand(
            id,
            request.OdometerStart,
            request.Items.Select(i => new InspectionItemDto(i.ItemName, i.Passed, i.Notes)).ToList()),
            cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "DriverOrAdmin")]
    [HttpPost]
    [Route("api/trips/{id:guid}/post-report")]
    public async Task<IActionResult> SubmitPostReport(
        Guid id,
        [FromBody] SubmitPostReportRequest request,
        CancellationToken cancellationToken)
    {
        await Sender.Send(new SubmitPostReportCommand(
            id,
            request.OdometerEnd,
            request.FuelAddedLitres,
            request.FuelCostDollars,
            request.HasIncident,
            request.IncidentType,
            request.IncidentDescription,
            request.AdditionalNotes,
            request.IsReadyToInvoice),
            cancellationToken);
        return NoContent();
    }

    [HttpGet]
    [Route("api/trips/{id:guid}/passengers")]
    public async Task<IActionResult> GetPassengers(Guid id, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetPassengersQuery(id), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/trips/{id:guid}/passengers")]
    public async Task<IActionResult> AddPassenger(
        Guid id,
        [FromBody] AddPassengerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new AddPassengerCommand(
            id,
            request.Name,
            request.ContactInfo,
            request.SeatNumber,
            request.PaymentStatus),
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/trips/{id:guid}/passengers/{passengerId:guid}")]
    public async Task<IActionResult> RemovePassenger(
        Guid id,
        Guid passengerId,
        CancellationToken cancellationToken)
    {
        await Sender.Send(new RemovePassengerCommand(id, passengerId), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/trips/{id:guid}/passengers/{passengerId:guid}/payment-status")]
    public async Task<IActionResult> UpdatePassengerPaymentStatus(
        Guid id,
        Guid passengerId,
        [FromBody] UpdatePassengerPaymentStatusRequest request,
        CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdatePassengerPaymentStatusCommand(id, passengerId, request.PaymentStatus), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateTripRequest(
    TripServiceType ServiceType,
    Guid? ClientId,
    Guid VehicleId,
    string? PurchaseOrderNumber,
    string? VehicleType,
    DateTime ScheduledAt,
    string? Notes,
    IReadOnlyList<StopRequestDto> Stops,
    int? SeatCapacity,
    decimal? PricePerSeat);

public sealed record UpdateTripRequest(
    Guid VehicleId,
    string? PurchaseOrderNumber,
    string? VehicleType,
    DateTime ScheduledAt,
    string? Notes,
    IReadOnlyList<StopRequestDto> Stops,
    int? SeatCapacity,
    decimal? PricePerSeat);

public sealed record StopRequestDto(int SequenceOrder, string LocationName, string? Address);

public sealed record AssignDriverRequest(Guid DriverId, string? VehicleType);

public sealed record UpdateStatusRequest(TripStatus Status);

public sealed record SubmitPreInspectionRequest(
    int OdometerStart,
    IReadOnlyList<InspectionItemRequestDto> Items);

public sealed record InspectionItemRequestDto(string ItemName, bool Passed, string? Notes);

public sealed record SubmitPostReportRequest(
    int OdometerEnd,
    decimal? FuelAddedLitres,
    decimal? FuelCostDollars,
    bool HasIncident,
    IncidentType? IncidentType,
    string? IncidentDescription,
    string? AdditionalNotes,
    bool IsReadyToInvoice);

public sealed record AddPassengerRequest(
    string Name,
    string? ContactInfo,
    int? SeatNumber,
    PassengerPaymentStatus PaymentStatus);

public sealed record UpdatePassengerPaymentStatusRequest(PassengerPaymentStatus PaymentStatus);

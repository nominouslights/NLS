using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Clients;
using ShuttleApi.Application.Common.Mediator;

namespace ShuttleApi.Api.Controllers;

[Authorize]
public sealed class ClientsController(ISender sender) : BaseApiController(sender)
{
    [HttpGet]
    [Route("api/clients")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetClientsQuery(), cancellationToken));

    [HttpGet]
    [Route("api/clients/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetClientByIdQuery(id), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/clients")]
    public async Task<IActionResult> Create([FromBody] CreateClientCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/clients/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateClientCommand(
            id,
            request.BusinessName,
            request.ServiceType,
            request.PrimaryContactName,
            request.PrimaryContactTitle,
            request.Phone,
            request.Email,
            request.StreetAddress,
            request.City,
            request.Province,
            request.PostalCode,
            request.GstHstNumber,
            request.PreferredPaymentMethod,
            request.NetPaymentTerms,
            request.ComplianceNotes,
            request.IsMinesite,
            request.IsActive,
            request.Industry,
            request.ProjectSite,
            request.NotificationEmails,
            request.TripDepartureArrivalEmails,
            request.PassengerBookingEmails), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/clients/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteClientCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet]
    [Route("api/clients/{clientId:guid}/contracts")]
    public async Task<IActionResult> GetContracts(Guid clientId, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetContractsByClientIdQuery(clientId), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/clients/{clientId:guid}/contracts")]
    public async Task<IActionResult> CreateContract(Guid clientId, [FromBody] CreateContractRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CreateContractCommand(
            clientId,
            request.StartDate,
            request.EndDate,
            request.Notes,
            request.RateLines), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/clients/{clientId:guid}/contracts/{id:guid}")]
    public async Task<IActionResult> UpdateContract(Guid id, [FromBody] UpdateContractRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdateContractCommand(id, request.StartDate, request.EndDate, request.Notes), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/clients/{clientId:guid}/contracts/{id:guid}/rates")]
    public async Task<IActionResult> AddRateLine(Guid id, [FromBody] AddRateLineCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command with { ContractId = id }, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/clients/{clientId:guid}/rates/{rateLineId:guid}")]
    public async Task<IActionResult> DeleteRateLine(Guid rateLineId, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteRateLineCommand(rateLineId), cancellationToken);
        return NoContent();
    }

    [HttpGet]
    [Route("api/clients/{clientId:guid}/rate-lines")]
    public async Task<IActionResult> GetRateLines(Guid clientId, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetRateLinesByClientQuery(clientId), cancellationToken));

    [HttpGet]
    [Route("api/clients/{clientId:guid}/email-templates")]
    public async Task<IActionResult> GetEmailTemplates(Guid clientId, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetClientEmailTemplatesQuery(clientId), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/clients/{clientId:guid}/email-templates/{type}")]
    public async Task<IActionResult> UpsertEmailTemplate(
        Guid clientId,
        ShuttleApi.Domain.Clients.ClientEmailTemplateType type,
        [FromBody] UpsertEmailTemplateRequest request,
        CancellationToken cancellationToken)
    {
        await Sender.Send(
            new UpsertClientEmailTemplateCommand(clientId, type, request.Subject, request.Body),
            cancellationToken);
        return NoContent();
    }

    [HttpGet]
    [Route("api/clients/{clientId:guid}/purchase-orders")]
    public async Task<IActionResult> GetPurchaseOrders(Guid clientId, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetPurchaseOrdersByClientIdQuery(clientId), cancellationToken));

    [HttpGet]
    [Route("api/clients/{clientId:guid}/purchase-orders/{id:guid}")]
    public async Task<IActionResult> GetPurchaseOrder(Guid clientId, Guid id, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetPurchaseOrderByIdQuery(clientId, id), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/clients/{clientId:guid}/purchase-orders")]
    public async Task<IActionResult> CreatePurchaseOrder(
        Guid clientId,
        [FromBody] UpsertPurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CreatePurchaseOrderCommand(
            clientId,
            request.PoNumber,
            request.StartDate,
            request.Details,
            request.LineItems,
            request.ContractIds), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/clients/{clientId:guid}/purchase-orders/{id:guid}")]
    public async Task<IActionResult> UpdatePurchaseOrder(
        Guid clientId,
        Guid id,
        [FromBody] UpsertPurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        await Sender.Send(new UpdatePurchaseOrderCommand(
            id,
            clientId,
            request.PoNumber,
            request.StartDate,
            request.Details,
            request.LineItems,
            request.ContractIds), cancellationToken);
        return NoContent();
    }
}

public sealed record UpsertEmailTemplateRequest(string Subject, string Body);

public sealed record UpdateClientRequest(
    string BusinessName,
    ShuttleApi.Domain.Clients.ServiceType ServiceType,
    string PrimaryContactName,
    string PrimaryContactTitle,
    string Phone,
    string Email,
    string StreetAddress,
    string City,
    string Province,
    string PostalCode,
    string? GstHstNumber,
    string PreferredPaymentMethod,
    int NetPaymentTerms,
    string? ComplianceNotes,
    bool IsMinesite,
    bool IsActive,
    string? Industry,
    string? ProjectSite,
    IReadOnlyList<string>? NotificationEmails,
    IReadOnlyList<string>? TripDepartureArrivalEmails,
    IReadOnlyList<string>? PassengerBookingEmails);

public sealed record CreateContractRequest(
    DateTime StartDate,
    DateTime EndDate,
    string? Notes,
    IReadOnlyList<RateLineDto> RateLines);

public sealed record UpdateContractRequest(
    DateTime StartDate,
    DateTime EndDate,
    string? Notes);

public sealed record UpsertPurchaseOrderRequest(
    string PoNumber,
    DateTime StartDate,
    string? Details,
    IReadOnlyList<PurchaseOrderLineItemDto> LineItems,
    IReadOnlyList<Guid>? ContractIds);

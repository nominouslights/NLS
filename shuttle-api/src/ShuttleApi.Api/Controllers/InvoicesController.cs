using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Billing;
using ShuttleApi.Application.Common.Mediator;

namespace ShuttleApi.Api.Controllers;

[Authorize]
public sealed class InvoicesController(ISender sender) : BaseApiController(sender)
{
    [HttpGet]
    [Route("api/invoices")]
    public async Task<IActionResult> List(
        [FromQuery] Guid clientId,
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new ListInvoicesByClientQuery(clientId, status), cancellationToken));

    [HttpGet]
    [Route("api/invoices/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetInvoiceByIdQuery(id), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/invoices")]
    public async Task<IActionResult> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreateInvoiceCommand(request.ClientId, request.TripIds, request.Notes),
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/invoices/{id:guid}")]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromBody] UpdateInvoiceDraftRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new UpdateInvoiceDraftCommand(id, request.DueDate, request.Notes, request.LineItems),
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/invoices/{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new MarkInvoiceSentCommand(id), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/invoices/{id:guid}/paid")]
    public async Task<IActionResult> MarkPaid(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new MarkInvoicePaidCommand(id), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/invoices/{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new VoidInvoiceCommand(id), cancellationToken);
        return Ok(result);
    }
}

public sealed record CreateInvoiceRequest(
    Guid ClientId,
    List<Guid> TripIds,
    string? Notes);

public sealed record UpdateInvoiceDraftRequest(
    DateTime DueDate,
    string? Notes,
    List<UpdateLineItemDto> LineItems);

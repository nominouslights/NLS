using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Billing;

public sealed record UpdateLineItemDto(
    Guid Id,
    string Description,
    decimal UnitRate,
    decimal Quantity);

public sealed record UpdateInvoiceDraftCommand(
    Guid InvoiceId,
    DateTime DueDate,
    string? Notes,
    List<UpdateLineItemDto> LineItems) : ICommand<InvoiceDetailResponse>;

using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Billing;

public sealed record ListInvoicesByClientQuery(
    Guid ClientId,
    string? Status) : IQuery<List<InvoiceListItemResponse>>;

using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Billing;

public sealed record MarkInvoicePaidCommand(Guid InvoiceId) : ICommand<InvoiceDetailResponse>;

using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Billing;

public sealed record MarkInvoiceSentCommand(Guid InvoiceId) : ICommand<InvoiceDetailResponse>;

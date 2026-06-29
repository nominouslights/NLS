using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Billing;

public sealed record VoidInvoiceCommand(Guid InvoiceId) : ICommand<InvoiceDetailResponse>;

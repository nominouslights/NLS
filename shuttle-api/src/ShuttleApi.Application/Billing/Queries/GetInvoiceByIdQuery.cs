using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Billing;

public sealed record GetInvoiceByIdQuery(Guid InvoiceId) : IQuery<InvoiceDetailResponse>;

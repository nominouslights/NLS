using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Billing;

public sealed record CreateInvoiceCommand(
    Guid ClientId,
    List<Guid> TripIds,
    string? Notes) : ICommand<InvoiceDetailResponse>;

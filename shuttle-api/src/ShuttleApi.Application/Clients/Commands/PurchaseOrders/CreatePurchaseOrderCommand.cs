using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record CreatePurchaseOrderCommand(
    Guid ClientId,
    string PoNumber,
    DateTime StartDate,
    string? Details,
    IReadOnlyList<PurchaseOrderLineItemDto> LineItems,
    IReadOnlyList<Guid>? ContractIds) : ICommand<CreatePurchaseOrderResult>;

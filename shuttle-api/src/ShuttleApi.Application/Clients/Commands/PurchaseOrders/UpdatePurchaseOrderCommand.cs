using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record UpdatePurchaseOrderCommand(
    Guid Id,
    Guid ClientId,
    string PoNumber,
    DateTime StartDate,
    string? Details,
    IReadOnlyList<PurchaseOrderLineItemDto> LineItems,
    IReadOnlyList<Guid>? ContractIds) : ICommand;

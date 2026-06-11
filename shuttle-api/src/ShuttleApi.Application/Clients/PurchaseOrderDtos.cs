namespace ShuttleApi.Application.Clients;

public sealed record PurchaseOrderLineItemDto(
    string Description,
    decimal UnitRate,
    decimal Quantity);

public sealed record PurchaseOrderLineItemResult(
    Guid Id,
    string Description,
    decimal UnitRate,
    decimal Quantity,
    decimal LineTotal,
    int SortOrder);

public sealed record PurchaseOrderSummaryResult(
    Guid Id,
    Guid ClientId,
    string PoNumber,
    DateTime StartDate,
    string? Details,
    decimal TotalValue,
    int LineItemCount,
    IReadOnlyList<Guid> LinkedContractIds);

public sealed record PurchaseOrderDetailResult(
    Guid Id,
    Guid ClientId,
    string PoNumber,
    DateTime StartDate,
    string? Details,
    decimal TotalValue,
    IReadOnlyList<PurchaseOrderLineItemResult> LineItems,
    IReadOnlyList<Guid> LinkedContractIds);

public sealed record CreatePurchaseOrderResult(Guid PurchaseOrderId);

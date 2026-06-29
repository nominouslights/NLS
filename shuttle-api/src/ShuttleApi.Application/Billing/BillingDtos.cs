namespace ShuttleApi.Application.Billing;

public record InvoiceListItemResponse(
    Guid Id,
    string InvoiceNumber,
    DateTime IssuedDate,
    DateTime DueDate,
    string Status,
    decimal TotalAmount);

public record InvoiceLineItemResponse(
    Guid Id,
    Guid? TripId,
    string LineType,
    string Description,
    decimal UnitRate,
    decimal Quantity,
    decimal LineTotal,
    int SortOrder);

public record InvoiceDetailResponse(
    Guid Id,
    Guid ClientId,
    string InvoiceNumber,
    string Status,
    DateTime IssuedDate,
    DateTime DueDate,
    string? Notes,
    DateTime? PaidAt,
    decimal SubTotal,
    decimal TotalAmount,
    List<InvoiceLineItemResponse> LineItems);

public record BillingReadyTripResponse(
    Guid Id,
    DateTime ScheduledAt,
    string? Direction,
    List<string> PassengerNames,
    decimal? TotalCargoWeightKg,
    string? CargoSummary);

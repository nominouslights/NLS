namespace NorthernLink.Fleet.Application.WorkOrders;

/// <summary>
/// The Fleet module's public representation of a work order. Status/Priority/Source travel
/// as their enum names (e.g. "InProgress", "PreTripInspection"); the frontend maps them.
/// <see cref="Defects"/> is the per-defect link (empty on manual and pre-link work orders).
/// </summary>
public sealed record WorkOrderResponse(
    Guid Id,
    Guid VehicleId,
    string Number,
    string Title,
    string Description,
    string Status,
    string Priority,
    string Source,
    string? SourceRef,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? AssignedTo,
    DateTimeOffset? DueDate,
    IReadOnlyList<string> LineItems,
    DateTimeOffset? CompletedAt,
    Guid? ResolvingServiceId,
    Guid? ShopId,
    decimal? AuthorizedLimitCad,
    string? BudgetCode,
    DateTimeOffset? DateRequiredOrOos,
    IReadOnlyList<WorkOrderDefectLineResponse> Defects);

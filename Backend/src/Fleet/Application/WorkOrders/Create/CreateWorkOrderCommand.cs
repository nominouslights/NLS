using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.Create;

/// <summary>
/// Creates a work order — manually, or generated from an inspection's defects
/// (<paramref name="InspectionId"/> links the two). Returns the new work order's id.
/// </summary>
public sealed record CreateWorkOrderCommand(
    Guid TenantId,
    Guid VehicleId,
    string Title,
    string? Description,
    WorkOrderPriority Priority,
    WorkOrderSource Source,
    string? SourceRef,
    string? AssignedTo,
    DateTimeOffset? DueDate,
    IReadOnlyList<string> LineItems,
    Guid? ShopId,
    decimal? AuthorizedLimitCad,
    string? BudgetCode,
    DateTimeOffset? DateRequiredOrOos,
    Guid? InspectionId) : ICommand<Guid>;

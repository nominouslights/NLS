using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.Create;

/// <summary>
/// Creates a work order — manually, or raised against inspection defects. Returns the new work
/// order's id.
///
/// <paramref name="Defects"/> names each defect by <c>(InspectionId, Item)</c>; every one must be
/// open, unattached, and reported against this work order's vehicle, and is attached to the new
/// work order. <paramref name="InspectionId"/> is the older whole-inspection form, kept working:
/// it means "every open, unattached defect of that inspection" and goes through the same path.
/// Both may be given; the union is attached.
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
    Guid? InspectionId,
    IReadOnlyList<WorkOrderDefectRef>? Defects = null) : ICommand<Guid>;

/// <summary>One defect, addressed the only way a defect can be: by its inspection and item.</summary>
public sealed record WorkOrderDefectRef(Guid InspectionId, string? Item);

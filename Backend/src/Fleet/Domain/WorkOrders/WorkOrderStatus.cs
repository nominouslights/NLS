namespace NorthernLink.Fleet.Domain.WorkOrders;

/// <summary>Work-order lifecycle. "InProgress"/"AwaitingParts" ↔ "In Progress"/"Awaiting Parts" on the UI.</summary>
public enum WorkOrderStatus
{
    Open,
    InProgress,
    AwaitingParts,
    Completed,
    Cancelled,
}

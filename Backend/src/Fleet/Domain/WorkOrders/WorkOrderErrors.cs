using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.WorkOrders;

/// <summary>All domain errors the WorkOrder aggregate (and its handlers) can produce.</summary>
public static class WorkOrderErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Fleet.WorkOrder.NotFound", "The work order was not found.");

    public static readonly Error TitleRequired = Error.Validation(
        "Fleet.WorkOrder.TitleRequired", "A work order title is required.");

    public static readonly Error Terminal = Error.Conflict(
        "Fleet.WorkOrder.Terminal", "A completed or cancelled work order can no longer change.");

    public static readonly Error UseCompleteEndpoint = Error.Validation(
        "Fleet.WorkOrder.UseCompleteEndpoint", "Complete a work order through its completion action so the resolving service is recorded.");
}

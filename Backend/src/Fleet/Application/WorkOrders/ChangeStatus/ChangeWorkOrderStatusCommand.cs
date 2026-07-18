using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.ChangeStatus;

/// <summary>Advances a work order's status (start, await parts, cancel — not complete).</summary>
public sealed record ChangeWorkOrderStatusCommand(Guid TenantId, Guid WorkOrderId, WorkOrderStatus Status) : ICommand;

using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.GetAll;

/// <summary>Lists every work order for the tenant (dashboard work-order queue).</summary>
public sealed record GetAllWorkOrdersQuery(Guid TenantId) : IQuery<IReadOnlyList<WorkOrderResponse>>;

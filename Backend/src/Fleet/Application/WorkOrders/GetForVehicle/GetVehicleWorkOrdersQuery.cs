using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.GetForVehicle;

/// <summary>Lists a vehicle's work orders.</summary>
public sealed record GetVehicleWorkOrdersQuery(Guid TenantId, Guid VehicleId)
    : IQuery<IReadOnlyList<WorkOrderResponse>>;

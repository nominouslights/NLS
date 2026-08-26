using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetVehicleStatus;

/// <summary>
/// The full computed PM schedule of one vehicle. An existing vehicle with no plan returns
/// the assigned-false shape; an unknown vehicle is a 404.
/// </summary>
public sealed record GetVehiclePmStatusQuery(Guid TenantId, Guid VehicleId)
    : IQuery<VehiclePmStatusResponse>;

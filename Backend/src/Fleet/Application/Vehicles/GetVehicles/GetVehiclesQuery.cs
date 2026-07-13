using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Vehicles;

namespace NorthernLink.Fleet.Application.Vehicles.GetVehicles;

/// <summary>Lists every vehicle visible to the current tenant, ordered by unit number.</summary>
public sealed record GetVehiclesQuery(Guid TenantId) : IQuery<IReadOnlyList<VehicleResponse>>;

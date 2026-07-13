using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Vehicles;

namespace NorthernLink.Fleet.Application.Vehicles.GetVehicleById;

/// <summary>Fetches a single vehicle by id (tenant-scoped).</summary>
public sealed record GetVehicleByIdQuery(Guid TenantId, Guid VehicleId) : IQuery<VehicleResponse>;

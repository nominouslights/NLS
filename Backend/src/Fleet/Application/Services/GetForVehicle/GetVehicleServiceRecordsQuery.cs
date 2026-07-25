using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Services;

namespace NorthernLink.Fleet.Application.Services.GetForVehicle;

/// <summary>Lists a vehicle's service history (most recent first).</summary>
public sealed record GetVehicleServiceRecordsQuery(Guid TenantId, Guid VehicleId)
    : IQuery<IReadOnlyList<ServiceRecordResponse>>;

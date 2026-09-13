using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.GetDefects;

/// <summary>
/// Lists a vehicle's defects, derived from its DVIRs. Open ones only unless
/// <paramref name="IncludeResolved"/> is set (the resolved history view).
/// </summary>
public sealed record GetVehicleDefectsQuery(Guid TenantId, Guid VehicleId, bool IncludeResolved)
    : IQuery<IReadOnlyList<VehicleDefectResponse>>;

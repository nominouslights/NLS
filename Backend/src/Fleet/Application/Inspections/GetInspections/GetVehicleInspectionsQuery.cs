using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.GetInspections;

/// <summary>
/// Lists inspections visible to the current tenant, newest first, optionally narrowed
/// to a vehicle unit.
/// </summary>
public sealed record GetVehicleInspectionsQuery(
    Guid TenantId,
    string? Unit = null) : IQuery<IReadOnlyList<VehicleInspectionResponse>>;

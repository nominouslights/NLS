using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetOverhauls;

/// <summary>The overhaul-early decision view for one vehicle — overhaul status plus related Test-item measurements.</summary>
public sealed record GetPmOverhaulsQuery(Guid TenantId, Guid VehicleId)
    : IQuery<PmOverhaulsResponse>;

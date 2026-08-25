using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetDue;

/// <summary>The shop-visit package for one vehicle — what is due soon or overdue, with total shop minutes.</summary>
public sealed record GetPmDueQuery(Guid TenantId, Guid VehicleId)
    : IQuery<PmDueResponse>;

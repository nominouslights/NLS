using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.GetAll;

/// <summary>Lists the tenant's maintenance plans (summary rows, ordered by name).</summary>
public sealed record GetMaintenancePlansQuery(Guid TenantId)
    : IQuery<IReadOnlyList<MaintenancePlanSummaryResponse>>;

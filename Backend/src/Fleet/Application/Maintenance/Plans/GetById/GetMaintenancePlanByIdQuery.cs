using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.GetById;

/// <summary>One maintenance plan in full — details plus every item and overhaul.</summary>
public sealed record GetMaintenancePlanByIdQuery(Guid TenantId, Guid PlanId)
    : IQuery<MaintenancePlanResponse>;

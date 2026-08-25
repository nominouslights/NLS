using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Application.Maintenance;

/// <summary>
/// Maps the maintenance domain records to their public response contracts. The read service
/// reuses these for the item/overhaul jsonb carried verbatim on the read models.
/// </summary>
public static class MaintenanceResponseMapper
{
    public static MaintenanceItemResponse ToResponse(MaintenanceItem item) => new(
        item.Code,
        item.System,
        item.Component,
        item.Tier.ToString(),
        item.Task.ToString(),
        item.IntervalKm,
        item.IntervalMonths,
        item.ShopMinutes,
        item.LeadKm,
        item.LeadDays,
        item.Notes);

    public static OverhaulSpecResponse ToResponse(OverhaulSpec overhaul) => new(
        overhaul.Code,
        overhaul.Component,
        overhaul.IntervalKm,
        overhaul.IntervalMonths,
        overhaul.LabourHours,
        overhaul.PartsCad,
        overhaul.LeadKm,
        overhaul.LeadDays,
        overhaul.Scope,
        overhaul.ConditionTriggers,
        overhaul.RelatedItemCodes);
}

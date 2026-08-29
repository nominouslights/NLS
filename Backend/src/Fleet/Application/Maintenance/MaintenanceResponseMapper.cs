using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Application.Maintenance;

/// <summary>
/// Maps the maintenance domain records to their public response contracts. The read service
/// reuses these for the item/overhaul jsonb carried verbatim on the read models — including
/// the computed-status shapes, so no field list is ever hand-copied in a query path.
/// </summary>
public static class MaintenanceResponseMapper
{
    /// <summary>One item line's computed due entry — spec fields from the plan, status from <see cref="PmSchedule.Compute"/>.</summary>
    public static PmEntryStatusResponse ToEntryStatus(
        MaintenanceItem item, int? lastDoneKm, DateOnly? lastDoneDate, PmDueStatus status) => new(
        item.Code,
        nameof(PmEntryKind.Item),
        item.System,
        item.Component,
        item.Tier.ToString(),
        item.Task.ToString(),
        item.IntervalKm,
        item.IntervalMonths,
        item.LeadKm,
        item.LeadDays,
        item.ShopMinutes,
        lastDoneKm,
        lastDoneDate,
        status.NextDueKm,
        status.NextDueDate,
        status.KmRemaining,
        status.DaysRemaining,
        status.State.ToString());

    /// <summary>One overhaul's computed due entry, under the synthetic Overhauls system heading.</summary>
    public static PmEntryStatusResponse ToEntryStatus(
        OverhaulSpec overhaul, int? lastDoneKm, DateOnly? lastDoneDate, PmDueStatus status) => new(
        overhaul.Code,
        nameof(PmEntryKind.Overhaul),
        PmEntryStatusResponse.OverhaulsSystemName,
        overhaul.Component,
        Tier: null,
        Task: null,
        overhaul.IntervalKm,
        overhaul.IntervalMonths,
        overhaul.LeadKm,
        overhaul.LeadDays,
        overhaul.ShopMinutes,
        lastDoneKm,
        lastDoneDate,
        status.NextDueKm,
        status.NextDueDate,
        status.KmRemaining,
        status.DaysRemaining,
        status.State.ToString());

    /// <summary>
    /// The overhaul-early decision row: spec fields straight from the plan, computed fields
    /// from the same <see cref="PmEntryStatusResponse"/> the status/due views emit — one
    /// compute path, no per-view drift.
    /// </summary>
    public static OverhaulStatusResponse ToOverhaulStatus(
        OverhaulSpec overhaul,
        PmEntryStatusResponse entry,
        IReadOnlyList<RelatedMeasurementResponse> relatedMeasurements) => new(
        overhaul.Code,
        overhaul.Component,
        overhaul.IntervalKm,
        overhaul.IntervalMonths,
        overhaul.LeadKm,
        overhaul.LeadDays,
        overhaul.LabourHours,
        overhaul.PartsCad,
        overhaul.Scope,
        overhaul.ConditionTriggers,
        entry.LastDoneKm,
        entry.LastDoneDate,
        entry.NextDueKm,
        entry.NextDueDate,
        entry.KmRemaining,
        entry.DaysRemaining,
        entry.State,
        relatedMeasurements);

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

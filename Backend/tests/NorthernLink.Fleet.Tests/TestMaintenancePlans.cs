using NorthernLink.Fleet.Domain.Maintenance;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>Factory helpers for maintenance plans in domain and handler tests.</summary>
internal static class TestMaintenancePlans
{
    public static MaintenanceItem Item(
        string code = "PM-E-001",
        string system = "Engine",
        string component = "Engine oil & filter",
        ComponentTier tier = ComponentTier.Primary,
        MaintenanceTask task = MaintenanceTask.Replace,
        int? intervalKm = 10_000,
        int? intervalMonths = 6,
        int shopMinutes = 45,
        int? leadKm = null,
        int? leadDays = null) => new()
    {
        Code = code,
        System = system,
        Component = component,
        Tier = tier,
        Task = task,
        IntervalKm = intervalKm,
        IntervalMonths = intervalMonths,
        ShopMinutes = shopMinutes,
        LeadKm = leadKm,
        LeadDays = leadDays,
    };

    public static OverhaulSpec Overhaul(
        string code = "OH-01",
        IReadOnlyList<string>? relatedItemCodes = null,
        int? intervalKm = 320_000,
        int? intervalMonths = 180,
        decimal partsCad = 6_500m,
        int? leadKm = null,
        int? leadDays = null,
        IReadOnlyList<string>? conditionTriggers = null) => new()
    {
        Code = code,
        Component = "Engine (3.7L Ti-VCT V6)",
        IntervalKm = intervalKm,
        IntervalMonths = intervalMonths,
        LabourHours = 40m,
        PartsCad = partsCad,
        LeadKm = leadKm,
        LeadDays = leadDays,
        Scope = "Teardown inspection or reman long-block.",
        ConditionTriggers = [.. conditionTriggers ?? ["Compression <85% of spec"]],
        RelatedItemCodes = [.. relatedItemCodes ?? []],
    };

    /// <summary>A valid plan with one item (PM-E-001) and one overhaul (OH-01).</summary>
    public static MaintenancePlan Create(
        Guid? tenantId = null,
        string name = "2016 Ford Transit T-150 / severe")
    {
        var result = MaintenancePlan.Create(
            tenantId ?? TestVehicles.TenantId,
            name,
            "2016 Ford Transit T-150",
            "severe",
            notes: null,
            [Item()],
            [Overhaul(relatedItemCodes: ["PM-E-001"])]);

        Assert.True(result.IsSuccess, $"Test plan creation failed: {result.Error.Code}");
        return result.Value;
    }
}

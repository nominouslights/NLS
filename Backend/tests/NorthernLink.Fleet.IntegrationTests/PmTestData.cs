using NorthernLink.Fleet.Application.Maintenance.Plans.SeedDefaults;
using NorthernLink.Fleet.Domain.Maintenance;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Builders for preventative-maintenance write-side rows. Plan names carry a unique suffix
/// because maintenance_plans has a unique (tenant_id, name) index and every test shares one
/// database — the same reasoning as <see cref="TestVehicleFactory"/>'s unit numbers.
/// </summary>
internal static class PmTestData
{
    public static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    public static MaintenanceItem Item(
        string code,
        string system,
        string component,
        int? intervalKm,
        int? intervalMonths,
        int shopMinutes,
        ComponentTier tier = ComponentTier.Primary,
        MaintenanceTask task = MaintenanceTask.Inspect) => new()
    {
        Code = code,
        System = system,
        Component = component,
        Tier = tier,
        Task = task,
        IntervalKm = intervalKm,
        IntervalMonths = intervalMonths,
        ShopMinutes = shopMinutes,
    };

    public static OverhaulSpec Overhaul(
        string code,
        string component,
        int? intervalKm,
        int? intervalMonths,
        decimal labourHours,
        decimal partsCad,
        params string[] relatedItemCodes) => new()
    {
        Code = code,
        Component = component,
        IntervalKm = intervalKm,
        IntervalMonths = intervalMonths,
        LabourHours = labourHours,
        PartsCad = partsCad,
        Scope = "Test overhaul scope.",
        RelatedItemCodes = [.. relatedItemCodes],
    };

    public static MaintenancePlan Plan(
        Guid tenantId,
        string name,
        IReadOnlyList<MaintenanceItem> items,
        IReadOnlyList<OverhaulSpec> overhauls)
    {
        var result = MaintenancePlan.Create(
            tenantId, name, "2016 Ford Transit T-150", "severe", notes: null, items, overhauls);
        Assert.True(result.IsSuccess, $"Test plan creation failed: {result.Error.Code}");
        return result.Value;
    }

    /// <summary>
    /// A plan carrying the full seeded default program (250 items + 10 overhauls) under a
    /// caller-chosen unique name — the realistic 260-line jsonb document, without colliding
    /// with the seed command's fixed tenant-unique name.
    /// </summary>
    public static MaintenancePlan SeedDataPlan(Guid tenantId, string name) =>
        Plan(tenantId, name, TransitSeverePlanSeed.BuildItems(), TransitSeverePlanSeed.BuildOverhauls());

    public static PlanAssignment Assign(Guid tenantId, Guid vehicleId, Guid planId)
    {
        var result = PlanAssignment.Assign(tenantId, vehicleId, planId);
        Assert.True(result.IsSuccess, $"Test assignment failed: {result.Error.Code}");
        return result.Value;
    }

    public static PmCompletion Completion(
        Guid tenantId,
        Guid vehicleId,
        Guid planId,
        string code,
        PmEntryKind kind,
        DateOnly performedAt,
        int odometerKm,
        string? measurement = null)
    {
        var result = PmCompletion.Log(
            tenantId, vehicleId, planId, code, kind, performedAt, odometerKm,
            performedBy: "R. Beardy", workOrderId: null, measurement, notes: null);
        Assert.True(result.IsSuccess, $"Test completion failed: {result.Error.Code}");
        return result.Value;
    }
}

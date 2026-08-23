using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Maintenance.Events;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class MaintenancePlanTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static MaintenanceItem Item(
        string code = "PM-E-001",
        int? intervalKm = 10_000,
        int? intervalMonths = 6,
        int shopMinutes = 45) => new()
    {
        Code = code,
        System = "Engine",
        Component = "Engine oil & filter",
        Tier = ComponentTier.Primary,
        Task = MaintenanceTask.Replace,
        IntervalKm = intervalKm,
        IntervalMonths = intervalMonths,
        ShopMinutes = shopMinutes,
    };

    private static OverhaulSpec Overhaul(
        string code = "OH-01",
        IReadOnlyList<string>? relatedItemCodes = null) => new()
    {
        Code = code,
        Component = "Engine (3.7L Ti-VCT V6)",
        IntervalKm = 320_000,
        IntervalMonths = 180,
        LabourHours = 40m,
        PartsCad = 6_500m,
        Scope = "Teardown inspection or reman long-block.",
        ConditionTriggers = ["Compression <85% of spec"],
        RelatedItemCodes = [.. relatedItemCodes ?? []],
    };

    private static Result<MaintenancePlan> Create(
        IReadOnlyList<MaintenanceItem>? items = null,
        IReadOnlyList<OverhaulSpec>? overhauls = null) => MaintenancePlan.Create(
            TenantId,
            "2016 Ford Transit T-150 / severe",
            "2016 Ford Transit T-150",
            "severe",
            notes: null,
            items ?? [Item()],
            overhauls ?? []);

    [Fact]
    public void Create_builds_a_plan_and_raises_the_created_event()
    {
        var result = Create(
            [Item("PM-E-001"), Item("PM-E-002")],
            [Overhaul("OH-01", ["PM-E-001", "PM-E-002"])]);

        Assert.True(result.IsSuccess);
        var plan = result.Value;
        Assert.Equal(TenantId, plan.TenantId);
        Assert.Equal("2016 Ford Transit T-150 / severe", plan.Name);
        Assert.Equal("2016 Ford Transit T-150", plan.VehicleModel);
        Assert.Equal("severe", plan.ServiceClass);
        Assert.Equal(2, plan.Items.Count);
        Assert.Single(plan.Overhauls);
        var evt = Assert.IsType<MaintenancePlanCreatedDomainEvent>(Assert.Single(plan.DomainEvents));
        Assert.Equal(plan.Id, evt.PlanId);
        Assert.Equal(TenantId, evt.TenantId);
    }

    [Fact]
    public void Create_rejects_a_duplicate_item_code()
    {
        var result = Create([Item("PM-E-001"), Item("PM-E-001")]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.DuplicateItemCode, result.Error);
    }

    [Fact]
    public void Create_rejects_an_item_with_no_interval_on_either_axis()
    {
        var result = Create([Item(intervalKm: null, intervalMonths: null)]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.ItemIntervalRequired, result.Error);
    }

    [Fact]
    public void Create_rejects_an_item_with_zero_shop_minutes()
    {
        var result = Create([Item(shopMinutes: 0)]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.InvalidShopMinutes, result.Error);
    }

    [Fact]
    public void Create_rejects_an_overhaul_referencing_an_unknown_item_code()
    {
        var result = Create(
            [Item("PM-E-001")],
            [Overhaul("OH-01", ["PM-E-999"])]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.UnknownRelatedItemCode, result.Error);
    }

    [Fact]
    public void Create_rejects_a_duplicate_overhaul_code()
    {
        var result = Create(
            [Item("PM-E-001")],
            [Overhaul("OH-01"), Overhaul("OH-01")]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.DuplicateOverhaulCode, result.Error);
    }

    [Fact]
    public void Update_replaces_the_items_wholesale_and_raises_the_updated_event()
    {
        var plan = Create([Item("PM-E-001"), Item("PM-E-002")]).Value;
        plan.ClearDomainEvents();

        var result = plan.Update(
            "2016 Ford Transit T-150 / severe",
            "2016 Ford Transit T-150",
            "severe",
            "Shortened intervals for gravel.",
            [Item("PM-E-003"), Item("PM-E-004"), Item("PM-E-005")],
            []);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, plan.Items.Count);
        Assert.DoesNotContain(plan.Items, i => i.Code is "PM-E-001" or "PM-E-002");
        Assert.Equal("Shortened intervals for gravel.", plan.Notes);
        var evt = Assert.IsType<MaintenancePlanUpdatedDomainEvent>(Assert.Single(plan.DomainEvents));
        Assert.Equal(plan.Id, evt.PlanId);
    }
}

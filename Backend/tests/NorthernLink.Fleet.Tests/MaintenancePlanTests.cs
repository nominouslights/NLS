using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Maintenance.Events;
using NorthernLink.Shared.Kernel;
using Xunit;
using static NorthernLink.Fleet.Tests.TestMaintenancePlans;

namespace NorthernLink.Fleet.Tests;

public class MaintenancePlanTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

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
    public void Create_normalizes_item_and_overhaul_text_before_storing()
    {
        var result = Create(
            [Item(" PM-E-001 ") with { System = " Engine ", Component = " Oil filter ", Notes = "   " }],
            [Overhaul(" OH-01 ", conditionTriggers: ["  Compression <85%  ", " ", ""])]);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("PM-E-001", item.Code);
        Assert.Equal("Engine", item.System);
        Assert.Equal("Oil filter", item.Component);
        Assert.Null(item.Notes);
        var overhaul = Assert.Single(result.Value.Overhauls);
        Assert.Equal("OH-01", overhaul.Code);
        Assert.Equal(new[] { "Compression <85%" }, overhaul.ConditionTriggers);
    }

    [Fact]
    public void Duplicate_detection_runs_on_trimmed_codes()
    {
        var result = Create([Item("PM-1"), Item("PM-1 ")]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.DuplicateItemCode, result.Error);
    }

    [Fact]
    public void Related_item_codes_match_after_trimming()
    {
        var result = Create(
            [Item("PM-E-001 ")],
            [Overhaul("OH-01", [" PM-E-001 "])]);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "PM-E-001" }, Assert.Single(result.Value.Overhauls).RelatedItemCodes);
    }

    [Fact]
    public void Create_rejects_a_plan_name_over_the_cap()
    {
        var result = MaintenancePlan.Create(
            TenantId,
            new string('x', MaintenancePlan.NameMaxLength + 1),
            "2016 Ford Transit T-150",
            "severe",
            notes: null,
            [Item()],
            []);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.NameTooLong, result.Error);
    }

    [Fact]
    public void Create_rejects_notes_over_the_cap()
    {
        var result = MaintenancePlan.Create(
            TenantId,
            "2016 Ford Transit T-150 / severe",
            "2016 Ford Transit T-150",
            "severe",
            new string('x', MaintenancePlan.NotesMaxLength + 1),
            [Item()],
            []);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.NotesTooLong, result.Error);
    }

    [Fact]
    public void Create_rejects_an_item_code_over_the_cap()
    {
        var result = Create([Item(new string('x', MaintenancePlan.CodeMaxLength + 1))]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.ItemCodeTooLong, result.Error);
    }

    [Fact]
    public void Create_rejects_an_overhaul_code_over_the_cap()
    {
        var result = Create(
            [Item()],
            [Overhaul(new string('x', MaintenancePlan.CodeMaxLength + 1))]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.OverhaulCodeTooLong, result.Error);
    }

    [Fact]
    public void Create_rejects_a_negative_parts_estimate()
    {
        var result = Create([Item()], [Overhaul(partsCad: -1m)]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.InvalidPartsCad, result.Error);
    }

    [Fact]
    public void Create_rejects_a_zero_lead_km_override()
    {
        var result = Create([Item(leadKm: 0)]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.InvalidLeadKm, result.Error);
    }

    [Fact]
    public void Create_rejects_a_zero_lead_days_override_on_an_overhaul()
    {
        var result = Create([Item()], [Overhaul(leadDays: 0)]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.InvalidLeadDays, result.Error);
    }

    [Fact]
    public void Create_accepts_positive_lead_overrides()
    {
        var result = Create([Item(leadKm: 500, leadDays: 7)], [Overhaul(leadDays: 90)]);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(500, item.LeadKm);
        Assert.Equal(7, item.LeadDays);
        Assert.Equal(90, Assert.Single(result.Value.Overhauls).LeadDays);
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
    public void Create_rejects_an_undefined_component_tier()
    {
        // JsonStringEnumConverter admits raw numbers, so an out-of-range integer can reach
        // the domain — it must not be stored.
        var result = Create([Item() with { Tier = (ComponentTier)99 }]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.InvalidComponentTier, result.Error);
    }

    [Fact]
    public void Create_rejects_an_undefined_maintenance_task()
    {
        var result = Create([Item() with { Task = (MaintenanceTask)99 }]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.InvalidMaintenanceTask, result.Error);
    }

    [Fact]
    public void Create_rejects_an_item_interval_km_over_the_cap()
    {
        var result = Create([Item(intervalKm: MaintenancePlan.MaxIntervalKm + 1)]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.IntervalKmTooLarge, result.Error);
    }

    [Fact]
    public void Create_rejects_an_overhaul_interval_months_over_the_cap()
    {
        var result = Create(
            [Item()],
            [Overhaul(intervalMonths: MaintenancePlan.MaxIntervalMonths + 1)]);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.IntervalMonthsTooLarge, result.Error);
    }

    [Fact]
    public void Create_accepts_intervals_at_the_caps()
    {
        var result = Create(
            [Item(intervalKm: MaintenancePlan.MaxIntervalKm, intervalMonths: MaintenancePlan.MaxIntervalMonths)]);

        Assert.True(result.IsSuccess);
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

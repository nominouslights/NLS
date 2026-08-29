using NorthernLink.Fleet.Application.Maintenance.Plans.SeedDefaults;
using NorthernLink.Fleet.Domain.Maintenance;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// Guards the transcribed "2016 Ford Transit T-150 / severe" seed data: the exact row
/// counts of the approved source tables (250 items / 10 overhauls, per prefix), that the
/// aggregate accepts the data wholesale (which exercises every plan invariant — code
/// uniqueness, interval caps, related-code resolution), and verbatim spot checks.
/// </summary>
public class TransitSeverePlanSeedTests
{
    private static MaintenancePlan CreateSeededPlan()
    {
        var result = MaintenancePlan.Create(
            TestVehicles.TenantId,
            TransitSeverePlanSeed.PlanName,
            TransitSeverePlanSeed.VehicleModel,
            TransitSeverePlanSeed.ServiceClass,
            notes: null,
            TransitSeverePlanSeed.BuildItems(),
            TransitSeverePlanSeed.BuildOverhauls());

        Assert.True(result.IsSuccess, $"Seed data rejected by MaintenancePlan.Create: {result.Error.Code}");
        return result.Value;
    }

    [Fact]
    public void The_seed_builds_a_valid_plan()
    {
        var plan = CreateSeededPlan();

        Assert.Equal("2016 Ford Transit T-150 / severe", plan.Name);
        Assert.Equal("2016 Ford Transit T-150", plan.VehicleModel);
        Assert.Equal("severe", plan.ServiceClass);
    }

    [Fact]
    public void The_seed_has_exactly_250_items_and_10_overhauls()
    {
        Assert.Equal(250, TransitSeverePlanSeed.BuildItems().Count);
        Assert.Equal(10, TransitSeverePlanSeed.BuildOverhauls().Count);
    }

    [Theory]
    [InlineData("E", 32)]
    [InlineData("C", 13)]
    [InlineData("F", 9)]
    [InlineData("EE", 12)]
    [InlineData("ISC", 14)]
    [InlineData("TD", 17)]
    [InlineData("RAD", 9)]
    [InlineData("S", 32)]
    [InlineData("B", 24)]
    [InlineData("WT", 14)]
    [InlineData("EL", 22)]
    [InlineData("H", 11)]
    [InlineData("BDG", 18)]
    [InlineData("ISP", 16)]
    [InlineData("CR", 7)]
    public void Item_counts_per_prefix_match_the_source_table(string prefix, int expected)
    {
        var count = TransitSeverePlanSeed.BuildItems()
            .Count(item => item.Code.Split('-')[1] == prefix);

        Assert.Equal(expected, count);
    }

    [Fact]
    public void The_fifteen_prefixes_are_the_only_ones_present()
    {
        var prefixes = TransitSeverePlanSeed.BuildItems()
            .Select(item => item.Code.Split('-')[1])
            .Distinct()
            .ToList();

        Assert.Equal(15, prefixes.Count);
    }

    [Fact]
    public void Overhaul_codes_run_OH01_through_OH10()
    {
        var codes = TransitSeverePlanSeed.BuildOverhauls().Select(o => o.Code).ToList();

        Assert.Equal(
            Enumerable.Range(1, 10).Select(n => $"OH-{n:00}").ToList(),
            codes);
    }

    [Fact]
    public void Every_overhaul_relates_to_at_least_one_existing_test_item()
    {
        var itemsByCode = TransitSeverePlanSeed.BuildItems().ToDictionary(item => item.Code);

        foreach (var overhaul in TransitSeverePlanSeed.BuildOverhauls())
        {
            Assert.True(overhaul.RelatedItemCodes.Count >= 1,
                $"{overhaul.Code} has no related item codes.");

            foreach (var code in overhaul.RelatedItemCodes)
            {
                Assert.True(itemsByCode.TryGetValue(code, out var item),
                    $"{overhaul.Code} references {code}, which is not a seeded item.");
                Assert.True(item!.Task == MaintenanceTask.Test,
                    $"{overhaul.Code} references {code}, whose task is {item.Task} — related " +
                    "items must be Test items (their measurements drive the overhaul-early decision).");
            }
        }
    }

    [Fact]
    public void Every_item_has_at_least_one_interval_and_positive_minutes_and_no_lead_overrides()
    {
        foreach (var item in TransitSeverePlanSeed.BuildItems())
        {
            Assert.True(item.IntervalKm is not null || item.IntervalMonths is not null,
                $"{item.Code} has no interval on either axis.");
            Assert.True(item.ShopMinutes > 0, $"{item.Code} has non-positive shop minutes.");
            Assert.Null(item.LeadKm);
            Assert.Null(item.LeadDays);
        }
    }

    [Fact]
    public void Spot_check_PM_E_001_the_severe_service_oil_change()
    {
        var item = TransitSeverePlanSeed.BuildItems().Single(i => i.Code == "PM-E-001");

        Assert.Equal("Engine", item.System);
        Assert.Equal("Engine oil & filter", item.Component);
        Assert.Equal(ComponentTier.Primary, item.Tier);
        Assert.Equal(MaintenanceTask.Replace, item.Task);
        Assert.Equal(10000, item.IntervalKm);
        Assert.Equal(6, item.IntervalMonths);
        Assert.Equal(45, item.ShopMinutes);
        Assert.Equal("Motorcraft 5W-20 synthetic blend; severe-service interval for gravel/cold", item.Notes);
    }

    [Theory]
    [InlineData("PM-ISC-007", 48)] // Battery: time-based, 4 years max in northern climate
    [InlineData("PM-B-014", 36)]   // Brake fluid: time-based, 3 years (DOT 4 LV)
    [InlineData("PM-WT-014", 6)]   // Winter/summer tire changeover: seasonal
    [InlineData("PM-CR-001", 12)]  // Manitoba annual safety inspection (MPI)
    public void Spot_check_time_based_items_have_no_km_axis(string code, int expectedMonths)
    {
        var item = TransitSeverePlanSeed.BuildItems().Single(i => i.Code == code);

        Assert.Null(item.IntervalKm);
        Assert.Equal(expectedMonths, item.IntervalMonths);
    }

    [Fact]
    public void Spot_check_OH_01_the_engine_overhaul()
    {
        var overhaul = TransitSeverePlanSeed.BuildOverhauls().Single(o => o.Code == "OH-01");

        Assert.Equal("Engine (3.7L Ti-VCT V6)", overhaul.Component);
        Assert.Equal(320000, overhaul.IntervalKm);
        Assert.Equal(180, overhaul.IntervalMonths);
        Assert.Equal(40m, overhaul.LabourHours);
        Assert.Equal(6500m, overhaul.PartsCad);
        Assert.Equal(new[] { "PM-E-026", "PM-E-027", "PM-E-028", "PM-E-029" }, overhaul.RelatedItemCodes);
        Assert.Equal(4, overhaul.ConditionTriggers.Count);
        Assert.Equal("Compression <85% of spec or >15% variance", overhaul.ConditionTriggers[0]);
        Assert.Equal("oil consumption >1L/1500km", overhaul.ConditionTriggers[3]);
    }

    [Fact]
    public void Condition_triggers_are_split_and_trimmed()
    {
        foreach (var overhaul in TransitSeverePlanSeed.BuildOverhauls())
        {
            Assert.NotEmpty(overhaul.ConditionTriggers);
            foreach (var trigger in overhaul.ConditionTriggers)
            {
                Assert.False(string.IsNullOrWhiteSpace(trigger));
                Assert.Equal(trigger.Trim(), trigger);
                Assert.DoesNotContain(';', trigger);
            }
        }
    }
}

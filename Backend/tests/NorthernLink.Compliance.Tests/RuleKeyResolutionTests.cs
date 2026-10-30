using System.Text.Json;
using NorthernLink.Compliance.Application.RuleSets;
using NorthernLink.Compliance.Domain.RuleSets;
using NorthernLink.Compliance.Infrastructure.Persistence;
using NorthernLink.Shared.HoursOfService;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// Every key in <see cref="RuleKeyCatalogue"/> resolves in the CA-MB rule set, and the set
/// exposes no key the catalogue does not name — so a client can rely on the catalogue as the
/// complete list, and the HOS section is provably the engine's numbers.
/// </summary>
public class RuleKeyResolutionTests
{
    private static readonly RuleSetDocument Current = CaMbRuleSets.Current;

    public static TheoryData<string> AllKeys()
    {
        var data = new TheoryData<string>();
        foreach (var key in RuleKeyCatalogue.All)
        {
            data.Add(key);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void Every_catalogued_key_resolves(string key)
    {
        var value = Current.Resolve(key);

        Assert.NotNull(value);
        Assert.Equal(key, value.Key);
        Assert.NotEmpty(value.Value);
        Assert.NotEmpty(value.Unit);
    }

    [Fact]
    public void The_flattened_set_and_the_catalogue_name_exactly_the_same_keys()
    {
        var flattened = Current.Flatten().Select(v => v.Key).ToList();

        Assert.Equal(flattened.Count, flattened.Distinct().Count());
        Assert.Equal(RuleKeyCatalogue.All.Order(), flattened.Order());
    }

    [Fact]
    public void The_HOS_section_mirrors_the_engine_constants()
    {
        Assert.Equal(HosLimits.DailyDrivingSeconds.ToString(), Current.Resolve("hos.dailyDrivingSeconds")!.Value);
        Assert.Equal(HosLimits.CycleOnDutySeconds.ToString(), Current.Resolve("hos.cycleOnDutySeconds")!.Value);
        Assert.Equal(HosLimits.PcDailyLimitDecikm.ToString(), Current.Resolve("hos.pcDailyLimitDecikm")!.Value);
        Assert.Equal(HosRuleSection.FromEngine(), Current.Hos);
    }

    [Fact]
    public void Retention_keys_reflect_the_catalogue_rows()
    {
        Assert.Equal("6", Current.Resolve("retention.R9.carrierMonths")!.Value);
        Assert.Equal("MonthEnd", Current.Resolve("retention.R9.anchor")!.Value);
        Assert.Equal("24", Current.Resolve("retention.R10.carrierMonths")!.Value);
        Assert.Equal("6", Current.Resolve("retention.R10.laterOf.vehicleLeftControlMonths")!.Value);
        Assert.Equal("true", Current.Resolve("retention.R21.permanent")!.Value);
        Assert.Null(Current.Resolve("retention.R21.carrierMonths"));
    }

    [Fact]
    public void The_named_values_from_the_plan_are_present()
    {
        Assert.Equal("2000000", Current.Resolve("insurance.minimumLiabilityCad")!.Value);
        Assert.Equal("160", Current.Resolve("thresholds.exemptionRadiusKm")!.Value);
        Assert.Equal("20", Current.Resolve("filing.driverForwardsRodsDays")!.Value);
        Assert.Equal("30", Current.Resolve("driverQualification.abstractWithinDaysBeforeHire")!.Value);
        Assert.Equal("9,4", Current.Resolve("inspection.seasonalInspectionMonths")!.Value);
        Assert.Null(Current.Resolve("hos.nonsense"));
    }

    [Fact]
    public void A_published_row_round_trips_to_the_same_document()
    {
        var published = RuleSet.Publish(Current, DateTimeOffset.UtcNow);

        Assert.Equal(Current.Flatten(), published.ToDocument().Flatten());
        Assert.Equal(Current.Jurisdiction, published.Jurisdiction);
        Assert.Equal(Current.Version, published.Version);
    }

    [Fact]
    public void Every_section_survives_the_jsonb_round_trip()
    {
        // The sections are stored one per jsonb column through JsonColumns.Options; a positional
        // record or a nested rule that STJ could not rebuild would surface here, not in production.
        Assert.Equal(Current.Hos, RoundTrip(Current.Hos));
        Assert.Equal(Current.Inspection.Flatten(), RoundTrip(Current.Inspection).Flatten());
        Assert.Equal(Current.DriverQualification, RoundTrip(Current.DriverQualification));
        Assert.Equal(Current.Filing, RoundTrip(Current.Filing));
        Assert.Equal(Current.Thresholds, RoundTrip(Current.Thresholds));
        Assert.Equal(Current.Insurance, RoundTrip(Current.Insurance));
        Assert.Equal(Current.Retention.Flatten(), RoundTrip(Current.Retention).Flatten());
    }

    [Fact]
    public async Task With_no_application_the_default_answers_and_says_so()
    {
        var resolver = new ActiveRuleSetResolver(new InMemoryReferenceStore(), new InMemoryRuleSetApplicationRepository());
        var handler = new GetRuleValueQueryHandler(resolver);

        var result = await handler.Handle(
            new GetRuleValueQuery(TestCompliance.TenantId, "hos.dailyDrivingSeconds", new DateOnly(2026, 10, 11)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RuleSource.Default, result.Value.Source);
        Assert.Equal(HosLimits.DailyDrivingSeconds.ToString(), result.Value.Value);
    }

    [Fact]
    public async Task An_unknown_key_is_a_not_found_with_a_basis()
    {
        var resolver = new ActiveRuleSetResolver(new InMemoryReferenceStore(), new InMemoryRuleSetApplicationRepository());
        var handler = new GetRuleValueQueryHandler(resolver);

        var result = await handler.Handle(
            new GetRuleValueQuery(TestCompliance.TenantId, "hos.nope", new DateOnly(2026, 10, 11)), CancellationToken.None);

        Assert.Equal("Compliance.RuleSet.RuleKeyUnknown", result.Error.Code);
    }

    private static T RoundTrip<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonColumns.Options), JsonColumns.Options)!;
}

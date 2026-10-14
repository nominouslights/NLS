using NorthernLink.Compliance.Domain.RuleSets;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// Rule-set version 2 adds the carrier-authority section; version 1 rows (published before it
/// existed, <c>authority</c> null) answer with the default, so every authority key resolves under
/// either version.
/// </summary>
public class AuthorityRuleSectionTests
{
    public const int DefaultTerm = 6;

    [Fact]
    public void Version_2_is_current_and_carries_the_authority_section()
    {
        var current = CaMbRuleSets.Current;

        Assert.Equal(2, current.Version);
        Assert.Equal(new DateOnly(2026, 10, 12), current.EffectiveFrom);
        Assert.NotNull(current.Authority);

        // Every version-1 value is unchanged (v1 flattens the default authority section too).
        Assert.Equal(CaMbRuleSets.V2026_10_01().Flatten(), current.Flatten());
    }

    [Fact]
    public void A_version_1_document_falls_back_to_the_default_authority_section()
    {
        var v1 = CaMbRuleSets.V2026_10_01();

        Assert.Null(v1.Authority);
        Assert.Same(AuthorityRuleSection.Default, v1.EffectiveAuthority);
        Assert.Equal(RuleKeyCatalogue.All.Order(), v1.Flatten().Select(v => v.Key).Order());
    }

    [Theory]
    [InlineData("authority.warningHorizonsDays", "7,14,30,60,90")]
    [InlineData("authority.sfcWarningDays", "60,30,14,7")]
    [InlineData("authority.insuranceWarningDays", "30,14,7")]
    [InlineData("authority.netCourseMaxDaysBeforeDesignation", "180")]
    [InlineData("authority.netRetrainingMonths", AuthorityRuleSection.NoInterval)]
    [InlineData("authority.registrationTermMonths", "6")]
    [InlineData("authority.planReacknowledgeWithinDays", "30")]
    [InlineData("authority.regulatedMinSeatingCapacity", "11")]
    public void The_authority_values_are_the_planned_defaults(string key, string value) =>
        Assert.Equal(value, CaMbRuleSets.Current.Resolve(key)!.Value);

    [Fact]
    public void A_published_row_rebuilds_the_same_document_including_the_section()
    {
        var row = RuleSet.Publish(CaMbRuleSets.Current, DateTimeOffset.UtcNow);

        Assert.Equal(CaMbRuleSets.Current.Flatten(), row.ToDocument().Flatten());
        Assert.Equal(AuthorityRuleSection.Default, row.Authority);
    }
}

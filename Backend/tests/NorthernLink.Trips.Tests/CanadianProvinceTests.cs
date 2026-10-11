using NorthernLink.Trips.Domain.Stops;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// <see cref="CanadianProvince"/> is the one spelling authority for provinces in the module —
/// every write normalises through it, so the operating-area gate compares codes, never text.
/// </summary>
public class CanadianProvinceTests
{
    [Fact]
    public void There_are_exactly_thirteen_codes_and_Manitoba_is_one_of_them()
    {
        Assert.Equal(13, CanadianProvince.Codes.Count);
        Assert.Equal(13, CanadianProvince.Codes.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(CanadianProvince.Manitoba, CanadianProvince.Codes);
        Assert.Equal("MB", CanadianProvince.Manitoba);
        Assert.All(CanadianProvince.Codes, code => Assert.True(CanadianProvince.IsCode(code)));
    }

    [Theory]
    [InlineData("MB", "MB")]
    [InlineData("mb", "MB")]
    [InlineData(" Mb ", "MB")]
    [InlineData("Manitoba", "MB")]
    [InlineData("MANITOBA", "MB")]
    [InlineData("Man.", "MB")]
    [InlineData("Saskatchewan", "SK")]
    [InlineData("Sask.", "SK")]
    [InlineData("SK", "SK")]
    [InlineData("Alberta", "AB")]
    [InlineData("Alta.", "AB")]
    [InlineData("British Columbia", "BC")]
    [InlineData("B.C.", "BC")]
    [InlineData("Ontario", "ON")]
    [InlineData("Ont.", "ON")]
    [InlineData("Quebec", "QC")]
    [InlineData("Québec", "QC")]
    [InlineData("P.Q.", "QC")]
    [InlineData("New Brunswick", "NB")]
    [InlineData("Nova Scotia", "NS")]
    [InlineData("Prince Edward Island", "PE")]
    [InlineData("P.E.I.", "PE")]
    [InlineData("PEI", "PE")]
    [InlineData("Newfoundland and Labrador", "NL")]
    [InlineData("Newfoundland & Labrador", "NL")]
    [InlineData("Nfld.", "NL")]
    [InlineData("Yukon", "YT")]
    [InlineData("Yukon Territory", "YT")]
    [InlineData("Northwest Territories", "NT")]
    [InlineData("N.W.T.", "NT")]
    [InlineData("NWT", "NT")]
    [InlineData("Nunavut", "NU")]
    public void Normalize_accepts_codes_names_and_the_usual_abbreviations_in_any_case(string typed, string code)
    {
        Assert.Equal(code, CanadianProvince.Normalize(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Minnesota")]
    [InlineData("MN")]
    [InlineData("Canada")]
    [InlineData("Manitoba, Canada")]
    [InlineData("M")]
    [InlineData("XX")]
    public void Normalize_returns_null_for_blank_or_unknown_values(string? typed)
    {
        Assert.Null(CanadianProvince.Normalize(typed));
    }

    [Fact]
    public void Every_code_normalises_to_itself_and_its_name_normalises_to_it()
    {
        foreach (var code in CanadianProvince.Codes)
        {
            Assert.Equal(code, CanadianProvince.Normalize(code));
            Assert.Equal(code, CanadianProvince.Normalize(CanadianProvince.NameOf(code)));
            Assert.NotEqual(code, CanadianProvince.NameOf(code));
        }
    }

    [Fact]
    public void NameOf_spells_out_the_province_and_echoes_an_unknown_code()
    {
        Assert.Equal("Manitoba", CanadianProvince.NameOf("MB"));
        Assert.Equal("Saskatchewan", CanadianProvince.NameOf("SK"));
        Assert.Equal("XX", CanadianProvince.NameOf("XX"));
    }

    [Fact]
    public void IsCode_is_case_sensitive_because_it_describes_the_stored_form()
    {
        Assert.True(CanadianProvince.IsCode("MB"));
        Assert.False(CanadianProvince.IsCode("mb"));
        Assert.False(CanadianProvince.IsCode("Manitoba"));
        Assert.False(CanadianProvince.IsCode(null));
    }

    [Theory]
    [InlineData(null, "MB")]
    [InlineData("", "MB")]
    [InlineData("  ", "MB")]
    [InlineData("Manitoba", "MB")]
    [InlineData("sk", "SK")]
    public void TryNormalizeOrDefault_treats_blank_as_Manitoba(string? typed, string expected)
    {
        Assert.True(CanadianProvince.TryNormalizeOrDefault(typed, out var code));
        Assert.Equal(expected, code);
    }

    [Fact]
    public void TryNormalizeOrDefault_refuses_an_unknown_value_without_defaulting_it()
    {
        Assert.False(CanadianProvince.TryNormalizeOrDefault("Minnesota", out var code));
        Assert.Null(code);
    }
}

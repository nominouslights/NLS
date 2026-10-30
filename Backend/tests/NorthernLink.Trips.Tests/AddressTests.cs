using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Stops;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class AddressTests
{
    [Fact]
    public void Create_accepts_a_full_address()
    {
        var result = Address.Create("123 Mystery Lake Rd", "Thompson", "Manitoba", "R8N 0M4", "Canada");

        Assert.True(result.IsSuccess);
        var address = result.Value;
        Assert.Equal("123 Mystery Lake Rd", address.Street);
        Assert.Equal("Thompson", address.City);
        // Stored as the code, whatever was typed — see Create_normalises_the_province_to_its_code.
        Assert.Equal("MB", address.Province);
        Assert.Equal("R8N 0M4", address.PostalCode);
        Assert.Equal("Canada", address.Country);
    }

    [Theory]
    [InlineData("Manitoba", "MB")]
    [InlineData("MB", "MB")]
    [InlineData("mb", "MB")]
    [InlineData("Man.", "MB")]
    [InlineData("Saskatchewan", "SK")]
    [InlineData("Ontario", "ON")]
    public void Create_normalises_the_province_to_its_code(string typed, string code)
    {
        var result = Address.Create(null, "Flin Flon", typed, null, "Canada");

        Assert.True(result.IsSuccess);
        Assert.Equal(code, result.Value.Province);
    }

    [Theory]
    [InlineData("Minnesota")]
    [InlineData("XX")]
    [InlineData("Manitoba, Canada")]
    public void Create_refuses_a_province_that_is_not_a_Canadian_province_or_territory(string typed)
    {
        var result = Address.Create(null, "Thompson", typed, null, "Canada");

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.Stop.UnknownProvince", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(typed, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_accepts_null_street_and_postal_code()
    {
        var result = Address.Create(null, "Lynn Lake", "Manitoba", null, "Canada");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Street);
        Assert.Null(result.Value.PostalCode);
        Assert.Equal("Lynn Lake", result.Value.City);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_city(string? city)
    {
        var result = Address.Create("123 Main St", city!, "Manitoba", "R8N 0M4", "Canada");

        Assert.True(result.IsFailure);
        Assert.Equal(StopErrors.CityRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_province(string? province)
    {
        var result = Address.Create("123 Main St", "Thompson", province!, "R8N 0M4", "Canada");

        Assert.True(result.IsFailure);
        Assert.Equal(StopErrors.ProvinceRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_country(string? country)
    {
        var result = Address.Create("123 Main St", "Thompson", "Manitoba", "R8N 0M4", country!);

        Assert.True(result.IsFailure);
        Assert.Equal(StopErrors.CountryRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_trims_all_values()
    {
        var result = Address.Create("  123 Main St  ", "  Thompson  ", "  Manitoba  ", "  R8N 0M4  ", "  Canada  ");

        Assert.True(result.IsSuccess);
        var address = result.Value;
        Assert.Equal("123 Main St", address.Street);
        Assert.Equal("Thompson", address.City);
        Assert.Equal("MB", address.Province);
        Assert.Equal("R8N 0M4", address.PostalCode);
        Assert.Equal("Canada", address.Country);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_maps_blank_optional_fields_to_null(string blank)
    {
        var result = Address.Create(blank, "Thompson", "Manitoba", blank, "Canada");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Street);
        Assert.Null(result.Value.PostalCode);
    }
}

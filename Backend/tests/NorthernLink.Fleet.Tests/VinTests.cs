using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class VinTests
{
    [Fact]
    public void Create_accepts_a_valid_17_character_vin()
    {
        var result = Vin.Create("1HVBBAAN8RH553310");

        Assert.True(result.IsSuccess);
        Assert.Equal("1HVBBAAN8RH553310", result.Value.Value);
    }

    [Fact]
    public void Create_normalizes_lowercase_and_whitespace()
    {
        var result = Vin.Create("  1hvbbaan8rh553310 ");

        Assert.True(result.IsSuccess);
        Assert.Equal("1HVBBAAN8RH553310", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1HVBBAAN8RH55331")] // 16 chars
    [InlineData("1HVBBAAN8RH5533100")] // 18 chars
    [InlineData("1HVBBAAN8RH55331I")] // contains I
    [InlineData("1HVBBAAN8RH55331O")] // contains O
    [InlineData("1HVBBAAN8RH55331Q")] // contains Q
    [InlineData("1HVBBAAN8RH55331-")] // punctuation
    public void Create_rejects_invalid_vins(string? value)
    {
        var result = Vin.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.InvalidVin, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Vins_with_the_same_value_are_equal()
    {
        Assert.Equal(Vin.Create("1HVBBAAN8RH553310").Value, Vin.Create("1hvbbaan8rh553310").Value);
    }
}

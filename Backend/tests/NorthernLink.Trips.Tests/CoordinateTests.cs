using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Stops;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class CoordinateTests
{
    [Fact]
    public void Create_accepts_a_valid_coordinate()
    {
        // Thompson, Manitoba.
        var result = Coordinate.Create(55.74, -97.85);

        Assert.True(result.IsSuccess);
        Assert.Equal(55.74, result.Value.Latitude);
        Assert.Equal(-97.85, result.Value.Longitude);
    }

    [Theory]
    [InlineData(91)]
    [InlineData(-91)]
    public void Create_rejects_out_of_range_latitude(double latitude)
    {
        var result = Coordinate.Create(latitude, -97.85);

        Assert.True(result.IsFailure);
        Assert.Equal(StopErrors.InvalidLatitude, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData(181)]
    [InlineData(-181)]
    public void Create_rejects_out_of_range_longitude(double longitude)
    {
        var result = Coordinate.Create(55.74, longitude);

        Assert.True(result.IsFailure);
        Assert.Equal(StopErrors.InvalidLongitude, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    [InlineData(90, -180)]
    [InlineData(-90, 180)]
    public void Create_accepts_boundary_values(double latitude, double longitude)
    {
        var result = Coordinate.Create(latitude, longitude);

        Assert.True(result.IsSuccess);
        Assert.Equal(latitude, result.Value.Latitude);
        Assert.Equal(longitude, result.Value.Longitude);
    }

    [Fact]
    public void Coordinates_with_the_same_values_are_equal()
    {
        Assert.Equal(Coordinate.Create(56.85, -101.05).Value, Coordinate.Create(56.85, -101.05).Value);
    }
}

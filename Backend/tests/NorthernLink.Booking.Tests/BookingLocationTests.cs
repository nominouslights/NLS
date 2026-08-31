using NorthernLink.Booking.Domain.Bookings;
using Xunit;

namespace NorthernLink.Booking.Tests;

/// <summary>BookingLocation value-object rules: stop reference needs a name; something must be present.</summary>
public class BookingLocationTests
{
    [Fact]
    public void Stop_reference_with_name_is_valid()
    {
        var stopId = Guid.NewGuid();

        var result = BookingLocation.Create(stopId, "  Thompson Depot  ", null);

        Assert.True(result.IsSuccess);
        Assert.Equal(stopId, result.Value.StopId);
        Assert.Equal("Thompson Depot", result.Value.StopName);
        Assert.Null(result.Value.AddressDetail);
    }

    [Fact]
    public void Address_detail_alone_is_valid()
    {
        var result = BookingLocation.Create(null, null, "12 Elm St, Lynn Lake");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.StopId);
        Assert.Null(result.Value.StopName);
        Assert.Equal("12 Elm St, Lynn Lake", result.Value.AddressDetail);
    }

    [Fact]
    public void Stop_name_alone_is_valid()
    {
        var result = BookingLocation.Create(null, "Leaf Rapids Stop", null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Stop_id_without_name_is_rejected()
    {
        var result = BookingLocation.Create(Guid.NewGuid(), "   ", "12 Elm St");

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.LocationStopNameRequired, result.Error);
    }

    [Fact]
    public void Empty_location_is_rejected()
    {
        var result = BookingLocation.Create(null, "  ", null);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.LocationRequired, result.Error);
    }

    [Fact]
    public void Structural_equality_holds()
    {
        var stopId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var a = BookingLocation.Create(stopId, "Thompson Depot", null).Value;
        var b = BookingLocation.Create(stopId, "Thompson Depot", null).Value;

        Assert.Equal(a, b);
    }
}

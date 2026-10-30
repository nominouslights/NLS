using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>The one stop-list reversal shared by generation, deadhead returns, the Bookeo import and trip edits.</summary>
public class RouteStopOrientationTests
{
    private static readonly Guid ThompsonId = Guid.NewGuid();

    // Deliberately out of Order in the list, to prove the helper sorts by Order, not position.
    private static List<RouteStop> Stops() =>
    [
        new RouteStop { Name = "Lynn Lake", Order = 2, OutboundOffsetMinutes = 105, ReturnOffsetMinutes = 0 },
        new RouteStop
        {
            StopId = ThompsonId, Name = "Thompson", Order = 0, Latitude = 55.74, Longitude = -97.86,
            OutboundOffsetMinutes = 0, ReturnOffsetMinutes = 110,
        },
        new RouteStop { Name = "Leaf Rapids", Order = 1, OutboundOffsetMinutes = 60, ReturnOffsetMinutes = 45 },
    ];

    [Fact]
    public void Reversed_runs_the_stops_backwards_and_resequences_only_Order()
    {
        var reversed = RouteStop.Reversed(Stops());

        Assert.Equal(["Lynn Lake", "Leaf Rapids", "Thompson"], reversed.Select(s => s.Name));
        Assert.Equal([0, 1, 2], reversed.Select(s => s.Order));
        // Both offsets stay attached to their own stop — never swapped.
        Assert.Equal([105, 60, 0], reversed.Select(s => s.OutboundOffsetMinutes!.Value));
        Assert.Equal([0, 45, 110], reversed.Select(s => s.ReturnOffsetMinutes!.Value));
        var thompson = reversed[2];
        Assert.Equal(ThompsonId, thompson.StopId);
        Assert.Equal(55.74, thompson.Latitude);
        Assert.Equal(-97.86, thompson.Longitude);
    }

    [Fact]
    public void Reversing_twice_restores_the_outbound_order()
    {
        var twice = RouteStop.Reversed(RouteStop.Reversed(Stops()));

        Assert.Equal(RouteStop.OrientedFor(Stops(), TripDirection.Outbound), twice);
    }

    [Fact]
    public void OrientedFor_is_ascending_for_outbound_and_unpaired_and_reversed_for_inbound()
    {
        string[] outbound = ["Thompson", "Leaf Rapids", "Lynn Lake"];

        Assert.Equal(outbound, RouteStop.OrientedFor(Stops(), TripDirection.Outbound).Select(s => s.Name));
        Assert.Equal(outbound, RouteStop.OrientedFor(Stops(), direction: null).Select(s => s.Name));
        Assert.Equal(
            RouteStop.Reversed(Stops()),
            RouteStop.OrientedFor(Stops(), TripDirection.Inbound));
    }

    [Fact]
    public void Reversed_does_not_mutate_its_input()
    {
        var stops = Stops();
        var snapshot = stops.ToList();

        _ = RouteStop.Reversed(stops);

        Assert.Equal(snapshot, stops);
    }
}

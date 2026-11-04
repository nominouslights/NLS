using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>StopTimetable: the one rule turning a trip's stop offsets into clock times.</summary>
public class StopTimetableTests
{
    private static readonly TimeOnly Morning = new(6, 30);

    /// <summary>Thompson → Leaf Rapids → Lynn Lake, both legs timed, with a 5-minute dwell at Leaf Rapids.</summary>
    private static List<RouteStop> OutboundOrdered() =>
    [
        new() { Name = "Thompson", Order = 0, OutboundDepartureOffsetMinutes = 0, ReturnDepartureOffsetMinutes = 110, ReturnArrivalOffsetMinutes = 110 },
        new() { Name = "Leaf Rapids", Order = 1, OutboundDepartureOffsetMinutes = 50, OutboundArrivalOffsetMinutes = 45, ReturnDepartureOffsetMinutes = 40, ReturnArrivalOffsetMinutes = 35 },
        new() { Name = "Lynn Lake", Order = 2, OutboundDepartureOffsetMinutes = 105, OutboundArrivalOffsetMinutes = 105, ReturnDepartureOffsetMinutes = 0 },
    ];

    [Fact]
    public void Outbound_times_come_from_the_outbound_pair()
    {
        var times = StopTimetable.Resolve(OutboundOrdered(), Morning, TripDirection.Outbound);

        Assert.Equal([0, 1, 2], times.Select(t => t.Order));
        Assert.Equal([new TimeOnly(6, 30), new TimeOnly(7, 20), new TimeOnly(8, 15)], times.Select(t => t.Departure!.Value));
        Assert.Equal([new TimeOnly(6, 30), new TimeOnly(7, 15), new TimeOnly(8, 15)], times.Select(t => t.Arrival!.Value));
    }

    [Fact]
    public void Inbound_uses_the_return_pair()
    {
        var reversed = RouteStop.Reversed(OutboundOrdered());

        var times = StopTimetable.Resolve(reversed, new TimeOnly(16, 0), TripDirection.Inbound);

        Assert.Equal([new TimeOnly(16, 0), new TimeOnly(16, 40), new TimeOnly(17, 50)], times.Select(t => t.Departure!.Value));
        Assert.Equal([new TimeOnly(16, 0), new TimeOnly(16, 35), new TimeOnly(17, 50)], times.Select(t => t.Arrival!.Value));
    }

    [Fact]
    public void An_untimed_leg_resolves_every_time_to_null()
    {
        List<RouteStop> stops =
        [
            new() { Name = "Thompson", Order = 0 },
            new() { Name = "Lynn Lake", Order = 1 },
        ];

        var times = StopTimetable.Resolve(stops, Morning, TripDirection.Outbound);

        Assert.Equal([0, 1], times.Select(t => t.Order));
        Assert.All(times, t => Assert.Equal((null, null), (t.Arrival, t.Departure)));
    }

    [Fact]
    public void A_null_arrival_equals_the_departure()
    {
        var times = StopTimetable.Resolve(OutboundOrdered(), Morning, TripDirection.Outbound);

        // Thompson carries no outbound arrival.
        Assert.Equal(times[0].Departure, times[0].Arrival);
    }

    [Fact]
    public void Times_wrap_past_midnight()
    {
        var times = StopTimetable.Resolve(OutboundOrdered(), new TimeOnly(23, 30), TripDirection.Outbound);

        Assert.Equal([new TimeOnly(23, 30), new TimeOnly(0, 20), new TimeOnly(1, 15)], times.Select(t => t.Departure!.Value));
        Assert.Equal(new TimeOnly(0, 15), times[1].Arrival);
    }

    [Fact]
    public void A_departure_column_that_does_not_ascend_along_order_is_treated_as_untimed()
    {
        // Outbound-ordered stops read with the return column: 110, 40, 0 — a trip whose stop
        // order does not match its direction. Nonsense times are worse than none.
        var times = StopTimetable.Resolve(OutboundOrdered(), Morning, TripDirection.Inbound);

        Assert.All(times, t => Assert.Equal((null, null), (t.Arrival, t.Departure)));
    }

    [Fact]
    public void A_partially_filled_departure_column_is_treated_as_untimed()
    {
        var stops = OutboundOrdered();
        stops[1] = stops[1] with { OutboundDepartureOffsetMinutes = null };

        var times = StopTimetable.Resolve(stops, Morning, TripDirection.Outbound);

        Assert.All(times, t => Assert.Null(t.Departure));
    }

    [Fact]
    public void A_trip_with_no_direction_reads_the_outbound_pair()
    {
        var times = StopTimetable.Resolve(OutboundOrdered(), Morning, direction: null);

        Assert.Equal([new TimeOnly(6, 30), new TimeOnly(7, 20), new TimeOnly(8, 15)], times.Select(t => t.Departure!.Value));
    }

    [Fact]
    public void The_result_is_ordered_by_order_whatever_the_input_order()
    {
        var shuffled = OutboundOrdered();
        shuffled.Reverse();

        var times = StopTimetable.Resolve(shuffled, Morning, TripDirection.Outbound);

        Assert.Equal([0, 1, 2], times.Select(t => t.Order));
        Assert.Equal(new TimeOnly(7, 20), times[1].Departure);
    }

    [Fact]
    public void An_arrival_after_its_own_departure_falls_back_to_the_departure()
    {
        var stops = OutboundOrdered();
        stops[1] = stops[1] with { OutboundArrivalOffsetMinutes = 60 };

        var times = StopTimetable.Resolve(stops, Morning, TripDirection.Outbound);

        Assert.Equal(new TimeOnly(7, 20), times[1].Arrival);
        Assert.Equal(new TimeOnly(7, 20), times[1].Departure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void An_arrival_at_or_before_the_previous_departure_falls_back_to_the_departure(int arrivalOffset)
    {
        var stops = OutboundOrdered();
        stops[1] = stops[1] with { OutboundArrivalOffsetMinutes = arrivalOffset };

        var times = StopTimetable.Resolve(stops, Morning, TripDirection.Outbound);

        Assert.Equal(new TimeOnly(7, 20), times[1].Arrival);
    }

    [Fact]
    public void A_nonzero_arrival_on_the_first_stop_falls_back_to_the_departure()
    {
        var stops = OutboundOrdered();
        stops[0] = stops[0] with { OutboundArrivalOffsetMinutes = 15 };

        var times = StopTimetable.Resolve(stops, Morning, TripDirection.Outbound);

        Assert.Equal(new TimeOnly(6, 30), times[0].Arrival);
    }

    [Fact]
    public void A_coherent_arrival_is_honoured()
    {
        var stops = OutboundOrdered();
        stops[0] = stops[0] with { OutboundArrivalOffsetMinutes = 0 };
        // Just after the previous departure is the earliest coherent arrival.
        stops[1] = stops[1] with { OutboundArrivalOffsetMinutes = 1 };

        var times = StopTimetable.Resolve(stops, Morning, TripDirection.Outbound);

        Assert.Equal([new TimeOnly(6, 30), new TimeOnly(6, 31), new TimeOnly(8, 15)], times.Select(t => t.Arrival!.Value));
    }
}

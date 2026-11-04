using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Domain.Trips;

/// <summary>
/// One stop's clock times on one trip. <see cref="Departure"/> is when the vehicle leaves the
/// stop (the pickup time), <see cref="Arrival"/> when it reaches it (the drop-off time). Both
/// are null when the trip's leg has no timetable.
/// </summary>
public sealed record ScheduledStopTime(int Order, TimeOnly? Arrival, TimeOnly? Departure);

/// <summary>
/// The one rule that turns a stop snapshot's timetable offsets into clock times. Every consumer
/// (pickup email, Driver Package, screens) reads the result off the trip response and never
/// does offset arithmetic itself. Times are derived at read time from fields the trip already
/// snapshots — its stops' offsets, <c>WindowStart</c> and <c>Direction</c> — so they follow an
/// edited window start and are never stored as a second copy that could go stale.
/// </summary>
public static class StopTimetable
{
    /// <summary>
    /// Resolves each stop's arrival and departure. <paramref name="direction"/> selects the
    /// offset pair: Inbound reads the Return pair, Outbound or none reads the Outbound pair (the
    /// rule <see cref="RouteStop"/> documents). Departure = <paramref name="windowStart"/> +
    /// departure offset; Arrival = <paramref name="windowStart"/> + (arrival offset ?? departure
    /// offset). <see cref="TimeOnly.AddMinutes(double)"/> wraps past midnight.
    /// <para>
    /// The leg is treated as untimed (all times null) when the selected departure column is
    /// empty, partially filled, or not strictly ascending along the stored <c>Order</c> — the
    /// last happens on a trip whose stop order does not match its direction (merged or unpaired
    /// legs), where any times would be nonsense. Never throws. Ordered by <c>Order</c>.
    /// </para>
    /// <para>
    /// On a timed leg each stop's arrival offset is honoured only when it is coherent: within
    /// (previous stop's departure, own departure] — on the first stop only when it is 0.
    /// Otherwise the stop falls back to arrival = departure. <c>Route.Validate</c> enforces the
    /// same rule on routes, but a free-form trip's stops (bound straight from the create/update
    /// trip request) never pass through it, and a hand-posted arrival later than its own
    /// departure would otherwise be emailed and printed.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ScheduledStopTime> Resolve(
        IEnumerable<RouteStop> stops, TimeOnly windowStart, TripDirection? direction)
    {
        var ordered = stops.OrderBy(stop => stop.Order).ToList();
        var inbound = direction == TripDirection.Inbound;
        var legs = ordered
            .Select(stop => inbound
                ? (Stop: stop, Arrival: stop.ReturnArrivalOffsetMinutes, Departure: stop.ReturnDepartureOffsetMinutes)
                : (Stop: stop, Arrival: stop.OutboundArrivalOffsetMinutes, Departure: stop.OutboundDepartureOffsetMinutes))
            .ToList();

        if (!IsTimed(legs.Select(leg => leg.Departure).ToList()))
        {
            return [.. ordered.Select(stop => new ScheduledStopTime(stop.Order, null, null))];
        }

        return [.. legs.Select((leg, index) =>
        {
            var departure = leg.Departure!.Value;
            var arrival = CoherentArrival(leg.Arrival, departure, index == 0 ? null : legs[index - 1].Departure!.Value);
            return new ScheduledStopTime(
                leg.Stop.Order,
                windowStart.AddMinutes(arrival),
                windowStart.AddMinutes(departure));
        })];
    }

    /// <summary>
    /// The arrival offset when it lies within (<paramref name="previousDeparture"/>,
    /// <paramref name="departure"/>] — or is exactly 0 on the first stop
    /// (<paramref name="previousDeparture"/> null) — else <paramref name="departure"/>.
    /// </summary>
    private static int CoherentArrival(int? arrival, int departure, int? previousDeparture)
    {
        if (arrival is not { } value)
        {
            return departure;
        }

        var coherent = previousDeparture is { } previous
            ? value > previous && value <= departure
            : value == 0;
        return coherent ? value : departure;
    }

    private static bool IsTimed(IReadOnlyList<int?> departures)
    {
        if (departures.Count == 0 || departures.Any(offset => offset is null))
        {
            return false;
        }

        for (var index = 1; index < departures.Count; index++)
        {
            if (departures[index] <= departures[index - 1])
            {
                return false;
            }
        }

        return true;
    }
}

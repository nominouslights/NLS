using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Application.Hos.Ledger;

/// <summary>
/// Segment arithmetic over one day's effective timeline, with no rule engine involved: the
/// stretch from each event to the next (or to the end of the day / now for the last one). Used
/// for the day summaries' segment list and for the roster rollup's driving figure — the engine
/// is the authority on totals; this is the shape a timeline renders as.
/// </summary>
public static class DutyDayTimeline
{
    public sealed record Segment(DutyStatus Status, DutyEventSource Source, long FromUnix, long ToUnix, Guid EventId)
    {
        public int DurationSeconds => (int)Math.Max(0, ToUnix - FromUnix);
    }

    /// <summary>
    /// Segments of the day clipped to [day start, min(day end, as-of)]. A day in progress ends
    /// its last segment at as-of; a completed day ends it at the day boundary.
    /// </summary>
    public static IReadOnlyList<Segment> Segments(DutyDay day, long asOfUnix)
    {
        var dayEnd = DutyLedgerAssembler.Calendar.DayEndUnix(day.LocalDate);
        var end = Math.Min(dayEnd, asOfUnix);
        var timeline = day.EffectiveTimeline();
        var segments = new List<Segment>();

        for (var i = 0; i < timeline.Count; i++)
        {
            var from = timeline[i].AtUtc.ToUnixTimeSeconds();
            var to = i + 1 < timeline.Count ? timeline[i + 1].AtUtc.ToUnixTimeSeconds() : end;
            to = Math.Min(to, end);
            if (to <= from)
            {
                continue;
            }

            segments.Add(new Segment(timeline[i].Status, timeline[i].Source, from, to, timeline[i].EventId));
        }

        return segments;
    }

    public static int DrivingSeconds(DutyDay day, long asOfUnix) =>
        Segments(day, asOfUnix).Where(s => s.Status == DutyStatus.Driving).Sum(s => s.DurationSeconds);

    /// <summary>The status the day ends in, or null for a day with no effective events.</summary>
    public static DutyStatus? LastStatus(DutyDay day)
    {
        var timeline = day.EffectiveTimeline();
        return timeline.Count == 0 ? null : timeline[^1].Status;
    }
}

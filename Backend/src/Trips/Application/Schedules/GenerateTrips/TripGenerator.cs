using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Application.Schedules.GenerateTrips;

/// <summary>
/// One leg a template wants materialized: the occurrence key (service date + direction —
/// the same pair the unique index guards), the leg's departure time, and the
/// <see cref="RoundTripKey"/> shared by an outbound/return pair (null for one-way
/// templates). Everything else on the trip is prefilled from the template and its route
/// by the caller.
/// </summary>
public sealed record TripDraft(
    DateOnly ServiceDate,
    TripDirection Direction,
    string? RoundTripKey,
    TimeOnly DepartureTime);

/// <summary>
/// The pure heart of trip generation — no clock, no database, fully unit-testable.
/// Expands a template over its generation horizon (today inclusive) per its
/// <see cref="ScheduleRecurrenceKind"/> — weekly days, an every-N-days interval, or
/// clamped monthly days — emitting an Outbound leg per matching date plus an Inbound
/// return leg when the template has a return departure, the pair sharing a RoundTripKey
/// (<c>{templateId:N}:{yyyyMMdd}</c>). Occurrences already materialized (passed in as
/// existing keys) are skipped, which makes generation idempotent; the database's unique
/// index on (tenant, template, date, direction) backstops races.
/// </summary>
public static class TripGenerator
{
    public static IReadOnlyList<TripDraft> Generate(
        ScheduleTemplate template,
        IReadOnlySet<(DateOnly ServiceDate, TripDirection Direction)> existingOccurrences,
        DateOnly today)
    {
        if (!template.Active)
        {
            return [];
        }

        var drafts = new List<TripDraft>();

        foreach (var date in MatchingDates(template, today))
        {
            var roundTripKey = template.ReturnDepartureTime is null
                ? null
                : RoundTripKeyFor(template.Id, date);

            if (!existingOccurrences.Contains((date, TripDirection.Outbound)))
            {
                drafts.Add(new TripDraft(date, TripDirection.Outbound, roundTripKey, template.DepartureTime));
            }

            if (template.ReturnDepartureTime is { } returnTime
                && !existingOccurrences.Contains((date, TripDirection.Inbound)))
            {
                drafts.Add(new TripDraft(date, TripDirection.Inbound, roundTripKey, returnTime));
            }
        }

        return drafts;
    }

    /// <summary>
    /// The service dates a template fires on within its horizon window
    /// <c>[today, today + GenerationHorizonDays)</c>, in ascending order with no duplicates.
    /// The branch matches <see cref="ScheduleTemplate.RecurrenceKind"/>; misconfigured
    /// templates (which validation prevents) simply yield nothing.
    /// </summary>
    private static IEnumerable<DateOnly> MatchingDates(ScheduleTemplate template, DateOnly today)
    {
        var windowEndExclusive = today.AddDays(template.GenerationHorizonDays);

        switch (template.RecurrenceKind)
        {
            case ScheduleRecurrenceKind.DaysOfWeek:
                for (var date = today; date < windowEndExclusive; date = date.AddDays(1))
                {
                    if (template.DaysOfWeek.Contains(date.DayOfWeek))
                    {
                        yield return date;
                    }
                }

                break;

            case ScheduleRecurrenceKind.EveryNDays:
                if (template.IntervalDays is { } interval and > 0 && template.AnchorDate is { } anchor)
                {
                    for (var date = today; date < windowEndExclusive; date = date.AddDays(1))
                    {
                        if (date >= anchor && (date.DayNumber - anchor.DayNumber) % interval == 0)
                        {
                            yield return date;
                        }
                    }
                }

                break;

            case ScheduleRecurrenceKind.MonthlyDays:
                if (template.DaysOfMonth.Count > 0)
                {
                    // Walk each month the window touches; a day > the month's length clamps to
                    // month-end (31 → 28/29/30), so two configured days can land on the same
                    // date — the HashSet keeps each service date to a single occurrence.
                    var emitted = new HashSet<DateOnly>();
                    for (var month = new DateOnly(today.Year, today.Month, 1);
                         month < windowEndExclusive;
                         month = month.AddMonths(1))
                    {
                        var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
                        foreach (var configuredDay in template.DaysOfMonth)
                        {
                            var date = new DateOnly(month.Year, month.Month, Math.Min(configuredDay, daysInMonth));
                            if (date >= today && date < windowEndExclusive && emitted.Add(date))
                            {
                                yield return date;
                            }
                        }
                    }
                }

                break;
        }
    }

    /// <summary>The key Billing groups by to price an outbound/return pair as one round trip.</summary>
    public static string RoundTripKeyFor(Guid templateId, DateOnly serviceDate) =>
        $"{templateId:N}:{serviceDate:yyyyMMdd}";
}

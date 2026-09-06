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
/// (<c>{templateId:N}:{yyyyMMdd}</c>).
/// <para>
/// The template's dated <see cref="ScheduleTemplate.Exceptions"/> are applied first:
/// a Skip removes the whole occurrence (both legs of a pair, including a next-day
/// return); an in-window ExtraRun adds an occurrence at the exception's own times (a
/// same-day return leg iff the exception has one — even on a one-way template), and when
/// it lands on a recurrence date the exception's times win; a TimeOverride keeps the
/// occurrence and swaps each set time in, unset fields falling back to the template's
/// (<see cref="ScheduleTemplate.ReturnNextDay"/> behaves exactly as without the override).
/// </para>
/// Occurrences already materialized (passed in as existing keys) are skipped, and no two
/// drafts in one batch ever share a (date, direction) key — an overnight return landing on
/// the next day's ExtraRun inbound would otherwise emit twice and blow the unique index —
/// which makes generation idempotent; the database's unique index on (tenant, template,
/// date, direction) backstops races. Exceptions are generation-time only: a trip already
/// materialized for a date is never cancelled or retimed from here.
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

        var windowEndExclusive = today.AddDays(template.GenerationHorizonDays);

        // At most one exception per date (an aggregate invariant), so a plain dictionary.
        var exceptionsByDate = template.Exceptions.ToDictionary(e => e.Date);

        // Occurrence dates = recurrence dates − Skip dates ∪ in-window ExtraRun dates.
        // The SortedSet both dedupes an extra run landing on a recurrence date and keeps
        // the drafts in ascending date order.
        var occurrenceDates = new SortedSet<DateOnly>(MatchingDates(template, today));
        foreach (var exception in template.Exceptions)
        {
            switch (exception.Kind)
            {
                case ScheduleExceptionKind.Skip:
                    occurrenceDates.Remove(exception.Date);
                    break;

                case ScheduleExceptionKind.ExtraRun
                    when exception.Date >= today && exception.Date < windowEndExclusive:
                    occurrenceDates.Add(exception.Date);
                    break;
            }
        }

        var drafts = new List<TripDraft>();

        // Seeded with the already-materialized keys, then claims each draft's key as it is
        // emitted, so one batch can never carry two drafts for the same (date, direction).
        // occurrenceDates iterates ascending, so Monday's overnight return (Tuesday Inbound)
        // is emitted before Tuesday's own ExtraRun same-day inbound — first-wins is
        // deterministic and the overnight return takes precedence.
        var emitted = new HashSet<(DateOnly ServiceDate, TripDirection Direction)>(existingOccurrences);

        foreach (var date in occurrenceDates)
        {
            exceptionsByDate.TryGetValue(date, out var exception);

            if (exception is { Kind: ScheduleExceptionKind.ExtraRun })
            {
                // The exception's times win outright (dedupe case included). Same-day only:
                // an extra run never spans midnight, whatever the template's own return does.
                var extraDeparture = exception.DepartureTime!.Value;
                var extraKey = exception.ReturnDepartureTime is null
                    ? null
                    : RoundTripKeyFor(template.Id, date);

                if (emitted.Add((date, TripDirection.Outbound)))
                {
                    drafts.Add(new TripDraft(date, TripDirection.Outbound, extraKey, extraDeparture));
                }

                if (exception.ReturnDepartureTime is { } extraReturn
                    && emitted.Add((date, TripDirection.Inbound)))
                {
                    drafts.Add(new TripDraft(date, TripDirection.Inbound, extraKey, extraReturn));
                }

                continue;
            }

            // A TimeOverride swaps in each time it sets; everything else — pairing, key
            // minting, ReturnNextDay — behaves exactly as an ordinary occurrence.
            var isOverride = exception is { Kind: ScheduleExceptionKind.TimeOverride };
            var departureTime = isOverride
                ? exception!.DepartureTime ?? template.DepartureTime
                : template.DepartureTime;

            var roundTripKey = template.ReturnDepartureTime is null
                ? null
                : RoundTripKeyFor(template.Id, date);

            if (emitted.Add((date, TripDirection.Outbound)))
            {
                drafts.Add(new TripDraft(date, TripDirection.Outbound, roundTripKey, departureTime));
            }

            if (template.ReturnDepartureTime is { } templateReturn)
            {
                var returnTime = isOverride
                    ? exception!.ReturnDepartureTime ?? templateReturn
                    : templateReturn;

                // An overnight route's return lands on the following calendar day — the
                // outbound and inbound legs share a RoundTripKey (minted off the outbound's
                // date) even though their ServiceDates differ.
                var returnDate = template.ReturnNextDay ? date.AddDays(1) : date;
                if (emitted.Add((returnDate, TripDirection.Inbound)))
                {
                    drafts.Add(new TripDraft(returnDate, TripDirection.Inbound, roundTripKey, returnTime));
                }
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

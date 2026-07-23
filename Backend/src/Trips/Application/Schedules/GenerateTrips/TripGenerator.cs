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
/// Expands a template's days-of-week over its generation horizon (today inclusive),
/// emitting an Outbound leg per matching day plus an Inbound return leg when the
/// template has a return departure, the pair sharing a RoundTripKey
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

        for (var offset = 0; offset < template.GenerationHorizonDays; offset++)
        {
            var date = today.AddDays(offset);
            if (!template.DaysOfWeek.Contains(date.DayOfWeek))
            {
                continue;
            }

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

    /// <summary>The key Billing groups by to price an outbound/return pair as one round trip.</summary>
    public static string RoundTripKeyFor(Guid templateId, DateOnly serviceDate) =>
        $"{templateId:N}:{serviceDate:yyyyMMdd}";
}

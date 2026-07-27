using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Read side for the trip activity timeline. Queries the module's <c>trips.event_journal</c>
/// via the base <see cref="EventJournalEntry"/> set — which every <c>ModuleDbContext</c>
/// already maps and tenant-filters — so no new table, migration, or write path is involved.
/// Read-only: only a filtered, no-tracking SELECT of the trip's and its manifest's rows.
/// Aggregate-type names come from <see cref="AuditNames"/> so they can never drift from what
/// the audit pipeline writes.
/// </summary>
internal sealed class TripActivityReadService(TripsDbContext context) : ITripActivityReadService
{
    private static readonly string TripAggregateType = AuditNames.ForAggregate(typeof(Trip));
    private static readonly string ManifestAggregateType = AuditNames.ForAggregate(typeof(TripManifest));

    public async Task<IReadOnlyList<TripActivityJournalEntry>> GetJournalEntriesAsync(
        Guid tripId,
        Guid? manifestId,
        CancellationToken cancellationToken = default)
    {
        var entries = await context.Set<EventJournalEntry>()
            .AsNoTracking()
            .Where(e =>
                (e.AggregateType == TripAggregateType && e.AggregateId == tripId) ||
                (manifestId != null && e.AggregateType == ManifestAggregateType && e.AggregateId == manifestId))
            .OrderBy(e => e.OccurredAtUtc)
            .Select(e => new TripActivityJournalEntry(e.OccurredAtUtc, e.AggregateType, e.EventType, e.Payload))
            .ToListAsync(cancellationToken);

        return entries;
    }
}
